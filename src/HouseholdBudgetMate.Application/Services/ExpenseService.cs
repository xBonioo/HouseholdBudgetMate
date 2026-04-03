using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class ExpenseService(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IExpenseService
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

        var expenses = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.MonthPlanId == monthPlan.Id)
            .OrderBy(x => x.Name)
            .Select(x => new ExpenseDto
            {
                Id = x.Id,
                MonthPlanId = x.MonthPlanId,
                Name = x.Name,
                CategoryId = x.CategoryId,
                CategoryName = x.Category.Name,
                TagId = x.TagId,
                TagName = x.Tag != null ? x.Tag.Name : null,
                PlannedAmount = x.PlannedAmount,
                ActualAmount = x.ActualAmount,
                ShowRemainingInUI = x.ShowRemainingInUI
            })
            .ToListAsync(cancellationToken);

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

        return new ExpenseDto
        {
            Id = expense.Id,
            MonthPlanId = expense.MonthPlanId,
            Name = expense.Name,
            CategoryId = expense.CategoryId,
            CategoryName = category.Name,
            TagId = expense.TagId,
            TagName = tag?.Name,
            PlannedAmount = expense.PlannedAmount,
            ActualAmount = expense.ActualAmount,
            ShowRemainingInUI = expense.ShowRemainingInUI
        };
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
        expense.ActualAmount = request.ActualAmount;
        expense.ShowRemainingInUI = request.ShowRemainingInUI;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ExpenseDto
        {
            Id = expense.Id,
            MonthPlanId = expense.MonthPlanId,
            Name = expense.Name,
            CategoryId = expense.CategoryId,
            CategoryName = category.Name,
            TagId = expense.TagId,
            TagName = tag?.Name,
            PlannedAmount = expense.PlannedAmount,
            ActualAmount = expense.ActualAmount,
            ShowRemainingInUI = expense.ShowRemainingInUI
        };
    }

    public async Task DeleteExpenseAsync(DeleteExpenseRequest request, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expense = await dbContext.Expenses
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Expense not found.");

        expense.IsDeleted = true;
        expense.DeletedAtUtc = DateTime.UtcNow;

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
}

