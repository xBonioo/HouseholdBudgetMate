using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class ExpenseService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider) : IExpenseService
{
    public async Task<IReadOnlyList<AvailableMonthDto>> GetAvailableMonthsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.MonthPlans
            .AsNoTracking()
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => new AvailableMonthDto
            {
                Year = x.Year,
                Month = x.Month
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MonthPlanDto> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        ValidateMonth(year, month);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, year, month, cancellationToken);

        var expenseEntities = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.MonthPlanId == monthPlan.Id)
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .Include(x => x.LineItems)
            .ThenInclude(x => x.Tag)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var expenses = expenseEntities
            .Select(MapExpenseToDto)
            .ToList();

        return new MonthPlanDto
        {
            Id = monthPlan.Id,
            Year = monthPlan.Year,
            Month = monthPlan.Month,
            IsClosed = monthPlan.IsClosed,
            Expenses = expenses
        };
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        ValidateMonth(request.Year, request.Month);

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new BadRequestException("Expense name is required.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, request.Year, request.Month, cancellationToken);

        var category = await dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category not found.");

        Tag? tag = null;
        if (request.TagId.HasValue)
        {
            tag = await dbContext.Tags
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.TagId.Value, cancellationToken)
                ?? throw new NotFoundException("Tag not found.");

            if (tag.CategoryId != request.CategoryId)
            {
                throw new BadRequestException("Selected tag does not belong to selected category.");
            }
        }

        var expense = new Expense
        {
            MonthPlanId = monthPlan.Id,
            Name = normalizedName,
            CategoryId = request.CategoryId,
            TagId = request.TagId,
            PlannedAmount = request.PlannedAmount,
            ActualAmount = request.ActualAmount,
            ShowRemainingInUI = request.ShowRemainingInUI
        };

        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildExpenseDtoAsync(dbContext, expense.Id, cancellationToken, category.Name, tag?.Name);
    }

    public async Task<ExpenseLineItemDto> CreateExpenseLineItemAsync(CreateExpenseLineItemRequest request, CancellationToken cancellationToken)
    {
        var normalizedDescription = request.Description.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            throw new BadRequestException("Line item description is required.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expense = await dbContext.Expenses
            .Include(x => x.Category)
            .Include(x => x.LineItems)
            .FirstOrDefaultAsync(x => x.Id == request.ExpenseId, cancellationToken)
            ?? throw new NotFoundException("Expense not found.");

        if (!expense.Category.SupportsLineItems)
        {
            throw new BadRequestException("Selected category does not support line items.");
        }

        Tag? tag = null;
        if (request.TagId.HasValue)
        {
            tag = await dbContext.Tags
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.TagId.Value, cancellationToken)
                ?? throw new NotFoundException("Tag not found.");

            if (tag.CategoryId != expense.CategoryId)
            {
                throw new BadRequestException("Selected tag does not belong to selected category.");
            }
        }

        var lineItem = new ExpenseLineItem
        {
            ExpenseId = expense.Id,
            Description = normalizedDescription,
            Amount = request.Amount,
            OccurredAt = request.OccurredAt,
            TagId = request.TagId
        };

        dbContext.ExpenseLineItems.Add(lineItem);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateActualAmountAsync(dbContext, expense.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ExpenseLineItemDto
        {
            Id = lineItem.Id,
            ExpenseId = lineItem.ExpenseId,
            Description = lineItem.Description,
            Amount = lineItem.Amount,
            OccurredAt = lineItem.OccurredAt,
            TagId = lineItem.TagId,
            TagName = tag?.Name
        };
    }

    public async Task<ExpenseLineItemDto> UpdateExpenseLineItemAsync(UpdateExpenseLineItemRequest request, CancellationToken cancellationToken)
    {
        var normalizedDescription = request.Description.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            throw new BadRequestException("Line item description is required.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var lineItem = await dbContext.ExpenseLineItems
            .Include(x => x.Expense)
            .ThenInclude(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Line item not found.");

        if (!lineItem.Expense.Category.SupportsLineItems)
        {
            throw new BadRequestException("Selected category does not support line items.");
        }

        Tag? tag = null;
        if (request.TagId.HasValue)
        {
            tag = await dbContext.Tags
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.TagId.Value, cancellationToken)
                ?? throw new NotFoundException("Tag not found.");

            if (tag.CategoryId != lineItem.Expense.CategoryId)
            {
                throw new BadRequestException("Selected tag does not belong to selected category.");
            }
        }

        lineItem.Description = normalizedDescription;
        lineItem.Amount = request.Amount;
        lineItem.OccurredAt = request.OccurredAt;
        lineItem.TagId = request.TagId;

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateActualAmountAsync(dbContext, lineItem.ExpenseId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ExpenseLineItemDto
        {
            Id = lineItem.Id,
            ExpenseId = lineItem.ExpenseId,
            Description = lineItem.Description,
            Amount = lineItem.Amount,
            OccurredAt = lineItem.OccurredAt,
            TagId = lineItem.TagId,
            TagName = tag?.Name,
        };
    }

    public async Task DeleteExpenseLineItemAsync(DeleteExpenseLineItemRequest request, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var lineItem = await dbContext.ExpenseLineItems
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Line item not found.");

        var expenseId = lineItem.ExpenseId;
        dbContext.ExpenseLineItems.Remove(lineItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        await RecalculateActualAmountAsync(dbContext, expenseId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExpenseDto> UpdateExpenseAsync(UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new BadRequestException("Expense name is required.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expense = await dbContext.Expenses
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Expense not found.");

        var category = await dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category not found.");

        Tag? tag = null;
        if (request.TagId.HasValue)
        {
            tag = await dbContext.Tags
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.TagId.Value, cancellationToken)
                ?? throw new NotFoundException("Tag not found.");

            if (tag.CategoryId != request.CategoryId)
            {
                throw new BadRequestException("Selected tag does not belong to selected category.");
            }
        }

        expense.Name = normalizedName;
        expense.CategoryId = request.CategoryId;
        expense.TagId = request.TagId;
        expense.PlannedAmount = request.PlannedAmount;
        if (!await dbContext.ExpenseLineItems.AnyAsync(x => x.ExpenseId == expense.Id, cancellationToken))
        {
            expense.ActualAmount = request.ActualAmount;
        }
        expense.ShowRemainingInUI = request.ShowRemainingInUI;

        await dbContext.SaveChangesAsync(cancellationToken);

        await RecalculateActualAmountAsync(dbContext, expense.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildExpenseDtoAsync(dbContext, expense.Id, cancellationToken, category.Name, tag?.Name);
    }

    public async Task DeleteExpenseAsync(DeleteExpenseRequest request, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expense = await dbContext.Expenses
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Expense not found.");

        expense.IsDeleted = true;
        expense.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateMonth(int year, int month)
    {
        if (year is < 2000 or > 3000)
        {
            throw new BadRequestException("Year is out of allowed range.");
        }

        if (month is < 1 or > 12)
        {
            throw new BadRequestException("Month must be in range 1..12.");
        }
    }

    private static async Task<MonthPlan> GetOrCreateMonthPlanAsync(
        ApplicationDbContext dbContext,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var monthPlan = await dbContext.MonthPlans
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month, cancellationToken);

        if (monthPlan is not null)
        {
            return monthPlan;
        }

        monthPlan = new MonthPlan
        {
            Year = year,
            Month = month,
            IsClosed = false
        };

        dbContext.MonthPlans.Add(monthPlan);
        await dbContext.SaveChangesAsync(cancellationToken);

        return monthPlan;
    }

    private static async Task RecalculateActualAmountAsync(
        ApplicationDbContext dbContext,
        int expenseId,
        CancellationToken cancellationToken)
    {
        var expense = await dbContext.Expenses
            .Include(x => x.LineItems)
            .FirstOrDefaultAsync(x => x.Id == expenseId, cancellationToken)
            ?? throw new NotFoundException("Expense not found.");

        if (expense.LineItems.Count == 0)
        {
            return;
        }

        expense.ActualAmount = expense.LineItems.Sum(x => x.Amount);
    }

    private static async Task<ExpenseDto> BuildExpenseDtoAsync(
        ApplicationDbContext dbContext,
        int expenseId,
        CancellationToken cancellationToken,
        string? categoryNameOverride = null,
        string? tagNameOverride = null)
    {
        var expense = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.Id == expenseId)
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .Include(x => x.LineItems)
            .ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Expense not found.");

        var dto = MapExpenseToDto(expense);

        if (!string.IsNullOrWhiteSpace(categoryNameOverride))
        {
            dto.CategoryName = categoryNameOverride;
        }

        if (dto.TagId.HasValue)
        {
            dto.TagName = tagNameOverride ?? dto.TagName;
        }

        return dto;
    }

    private static ExpenseDto MapExpenseToDto(Expense expense)
    {
        return new ExpenseDto
        {
            Id = expense.Id,
            MonthPlanId = expense.MonthPlanId,
            Name = expense.Name,
            CategoryId = expense.CategoryId,
            CategoryName = expense.Category.Name,
            TagId = expense.TagId,
            TagName = expense.Tag?.Name,
            PlannedAmount = expense.PlannedAmount,
            ActualAmount = expense.LineItems.Count > 0 ? expense.LineItems.Sum(li => li.Amount) : expense.ActualAmount,
            SupportsLineItems = expense.Category.SupportsLineItems,
            ShowRemainingInUI = expense.ShowRemainingInUI,
            LineItems = expense.LineItems
                .OrderByDescending(li => li.OccurredAt)
                .ThenBy(li => li.Id)
                .Select(MapLineItemToDto)
                .ToList()
        };
    }

    private static ExpenseLineItemDto MapLineItemToDto(ExpenseLineItem lineItem)
    {
        return new ExpenseLineItemDto
        {
            Id = lineItem.Id,
            ExpenseId = lineItem.ExpenseId,
            Description = lineItem.Description,
            Amount = lineItem.Amount,
            OccurredAt = lineItem.OccurredAt,
            TagId = lineItem.TagId,
            TagName = lineItem.Tag?.Name
        };
    }
}

