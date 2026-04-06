using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Application.Mapping;
using HouseholdBudgetMate.Application.Validation;
using HouseholdBudgetMate.Application.Validation.Common;
using HouseholdBudgetMate.Application.Validation.Expenses;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class ExpenseService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider) : IExpenseService
{
    private static readonly YearMonthRequestValidator YearMonthValidator = new();
    private static readonly DateInMonthRequestValidator DateInMonthValidator = new();

    private static readonly CreateMonthSavingsTransferItemRequestValidator CreateSavingsTransferItemValidator = new();
    private static readonly CreateExpenseRequestValidator CreateExpenseValidator = new();
    private static readonly CreateExpenseLineItemRequestValidator CreateExpenseLineItemValidator = new();
    private static readonly UpdateMonthSavingsTransferRequestValidator UpdateSavingsTransferValidator = new();
    private static readonly UpdateMonthSavingsTransferItemRequestValidator UpdateSavingsTransferItemValidator = new();
    private static readonly UpdateExpenseRequestValidator UpdateExpenseValidator = new();
    private static readonly ReorderExpensesRequestValidator ReorderExpensesValidator = new();
    private static readonly UpdateExpenseLineItemRequestValidator UpdateExpenseLineItemValidator = new();
    private static readonly DeleteMonthSavingsTransferItemRequestValidator DeleteSavingsTransferItemValidator = new();
    private static readonly DeleteExpenseRequestValidator DeleteExpenseValidator = new();
    private static readonly DeleteExpenseLineItemRequestValidator DeleteExpenseLineItemValidator = new();

    public async Task<IReadOnlyList<AvailableMonthDto>> GetAvailableMonthsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlans = await dbContext.MonthPlans
            .AsNoTracking()
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => x.MapAvailableMonthToDto())
            .ToListAsync(cancellationToken);

        return monthPlans;
    }

    public async Task<MonthPlanDto> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, year, month, cancellationToken);

        var expenseEntities = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.MonthPlanId == monthPlan.Id)
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .Include(x => x.LineItems)
            .ThenInclude(x => x.Tag)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var expenses = expenseEntities
            .Select(x => x.MapExpenseToDto())
            .ToList();

        var savingsTransfers = await dbContext.MonthSavingsTransferItems
            .AsNoTracking()
            .Where(x => x.MonthPlanId == monthPlan.Id)
            .OrderBy(x => x.TransferDate)
            .ThenBy(x => x.Id)
            .Select(x => x.MapSavingsTransferToDto())
            .ToListAsync(cancellationToken);

        return BuildMonthPlanDto(monthPlan, expenses, savingsTransfers);
    }

    public async Task<MonthSavingsTransferItemDto> CreateMonthSavingsTransferItemAsync(
        CreateMonthSavingsTransferItemRequest request, CancellationToken cancellationToken)
    {
        CreateSavingsTransferItemValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, request.Year, request.Month, cancellationToken);

        var item = new MonthSavingsTransferItem
        {
            MonthPlanId = monthPlan.Id,
            Amount = request.Amount,
            TransferDate = request.TransferDate
        };

        dbContext.MonthSavingsTransferItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item.MapSavingsTransferToDto();
    }

    public async Task<MonthSavingsTransferItemDto> UpdateMonthSavingsTransferItemAsync(
        UpdateMonthSavingsTransferItemRequest request, CancellationToken cancellationToken)
    {
        UpdateSavingsTransferItemValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var item = await dbContext.MonthSavingsTransferItems
                       .Include(x => x.MonthPlan)
                       .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                   ?? throw new NotFoundException("Savings transfer item not found.");

        DateInMonthValidator.ValidateOrThrowBadRequest(new DateInMonthRequest(
            request.TransferDate,
            item.MonthPlan.Year,
            item.MonthPlan.Month,
            "Savings transfer date must belong to selected month and year."));

        item.Amount = request.Amount;
        item.TransferDate = request.TransferDate;
        await dbContext.SaveChangesAsync(cancellationToken);

        return item.MapSavingsTransferToDto();
    }

    public async Task DeleteMonthSavingsTransferItemAsync(DeleteMonthSavingsTransferItemRequest request,
        CancellationToken cancellationToken)
    {
        DeleteSavingsTransferItemValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var item = await dbContext.MonthSavingsTransferItems
                       .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                   ?? throw new NotFoundException("Savings transfer item not found.");

        dbContext.MonthSavingsTransferItems.Remove(item);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        CreateExpenseValidator.ValidateOrThrowBadRequest(request);

        var normalizedName = request.Name;

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
            Order = await dbContext.Expenses
                .Where(x => x.MonthPlanId == monthPlan.Id)
                .Select(x => (int?)x.Order)
                .MaxAsync(cancellationToken) + 1 ?? 1,
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

    public async Task ReorderExpensesAsync(ReorderExpensesRequest request, CancellationToken cancellationToken)
    {
        ReorderExpensesValidator.ValidateOrThrowBadRequest(request);

        if (request.ExpenseIds.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expenses = await dbContext.Expenses
            .Where(x => request.ExpenseIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (expenses.Count != request.ExpenseIds.Count)
        {
            throw new BadRequestException("Some expenses were not found.");
        }

        var monthPlanIds = expenses.Select(x => x.MonthPlanId).Distinct().ToList();
        if (monthPlanIds.Count != 1)
        {
            throw new BadRequestException("Expenses must belong to one month plan.");
        }

        for (var i = 0; i < request.ExpenseIds.Count; i++)
        {
            var expense = expenses.First(x => x.Id == request.ExpenseIds[i]);
            expense.Order = i + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExpenseLineItemDto> CreateExpenseLineItemAsync(CreateExpenseLineItemRequest request,
        CancellationToken cancellationToken)
    {
        CreateExpenseLineItemValidator.ValidateOrThrowBadRequest(request);

        var normalizedDescription = request.Description;

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

    public async Task<ExpenseLineItemDto> UpdateExpenseLineItemAsync(UpdateExpenseLineItemRequest request,
        CancellationToken cancellationToken)
    {
        UpdateExpenseLineItemValidator.ValidateOrThrowBadRequest(request);

        var normalizedDescription = request.Description;

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

    public async Task DeleteExpenseLineItemAsync(DeleteExpenseLineItemRequest request,
        CancellationToken cancellationToken)
    {
        DeleteExpenseLineItemValidator.ValidateOrThrowBadRequest(request);

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
        UpdateExpenseValidator.ValidateOrThrowBadRequest(request);

        var normalizedName = request.Name;

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
        DeleteExpenseValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expense = await dbContext.Expenses
                          .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                      ?? throw new NotFoundException("Expense not found.");

        expense.IsDeleted = true;
        expense.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();

        await dbContext.SaveChangesAsync(cancellationToken);
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

    private static MonthPlanDto BuildMonthPlanDto(
        MonthPlan monthPlan,
        IReadOnlyList<ExpenseDto> expenses,
        IReadOnlyList<MonthSavingsTransferItemDto> savingsTransfers)
    {
        return new MonthPlanDto
        {
            Id = monthPlan.Id,
            Year = monthPlan.Year,
            Month = monthPlan.Month,
            IsClosed = monthPlan.IsClosed,
            Kpi = CalculateMonthPlanKpi(expenses),
            SavingsTransfers = savingsTransfers,
            Expenses = expenses
        };
    }

    private static MonthPlanKpiDto CalculateMonthPlanKpi(IReadOnlyList<ExpenseDto> expenses)
    {
        var plannedTotal = expenses.Sum(x => x.PlannedAmount);
        var spentTotal = expenses.Sum(x => x.ActualAmount);

        var remainingFromVisibleExpenses = expenses
            .Where(x => x.ShowRemainingInUI && !IsDerivedUnplanned(x.PlannedAmount))
            .Sum(x => Math.Max(0, x.RemainingAmount));

        // Doliczamy plan tylko dla pozycji bez pokazywania "pozostalo", aby uniknac podwojnego liczenia.
        var plannedForMissingActualHiddenInUi = expenses
            .Where(x => !x.ShowRemainingInUI)
            .Where(x => !IsDerivedUnplanned(x.PlannedAmount))
            .Where(x => x.ActualAmount == 0)
            .Sum(x => x.PlannedAmount);

        var remainingTotal = remainingFromVisibleExpenses + plannedForMissingActualHiddenInUi;
        var remainingPercent = plannedTotal <= 0
            ? 0
            : Math.Clamp((double)(remainingTotal / plannedTotal * 100), 0, 100);

        return new MonthPlanKpiDto
        {
            PlannedTotal = plannedTotal,
            SpentTotal = spentTotal,
            RemainingTotal = remainingTotal,
            RemainingPercent = remainingPercent
        };
    }

    private static bool IsDerivedUnplanned(decimal? plannedAmount)
    {
        return !plannedAmount.HasValue || plannedAmount <= 0;
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

        var dto = expense.MapExpenseToDto();

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
}