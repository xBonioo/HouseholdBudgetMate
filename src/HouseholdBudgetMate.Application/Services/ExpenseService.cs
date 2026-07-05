using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Facility.Events;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Helpers;
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
    IDateTimeProvider dateTimeProvider,
    IAppEventPublisher appEventPublisher,
    IIncomeService incomeService,
    ILoanService loanService) : IExpenseService
{
    private static readonly YearMonthRequestValidator YearMonthValidator = new();
    private static readonly DateInMonthRequestValidator DateInMonthValidator = new();

    private static readonly CreateMonthSavingsTransferItemRequestValidator CreateSavingsTransferItemValidator = new();
    private static readonly CreateExpenseRequestValidator CreateExpenseValidator = new();
    private static readonly CreateExpenseLineItemRequestValidator CreateExpenseLineItemValidator = new();
    private static readonly UpdateMonthSavingsTransferItemRequestValidator UpdateSavingsTransferItemValidator = new();
    private static readonly UpdateExpenseRequestValidator UpdateExpenseValidator = new();
    private static readonly ReorderExpensesRequestValidator ReorderExpensesValidator = new();
    private static readonly ApplyMonthPlanSuggestionsRequestValidator ApplyMonthPlanSuggestionsValidator = new();
    private static readonly CopySelectedExpensesToMonthRequestValidator CopySelectedExpensesToMonthValidator = new();
    private static readonly CopySelectedExpensesToNextMonthRequestValidator CopySelectedExpensesToNextMonthValidator = new();
    private static readonly UpsertAnnualPlanRequestValidator UpsertAnnualPlanValidator = new();
    private static readonly UpdateExpenseLineItemRequestValidator UpdateExpenseLineItemValidator = new();
    private static readonly DeleteMonthSavingsTransferItemRequestValidator DeleteSavingsTransferItemValidator = new();
    private static readonly DeleteExpenseRequestValidator DeleteExpenseValidator = new();
    private static readonly DeleteExpenseLineItemRequestValidator DeleteExpenseLineItemValidator = new();
    private static readonly CreateRegularExpenseDefinitionRequestValidator CreateRegularExpenseDefinitionValidator = new();
    private static readonly UpdateRegularExpenseDefinitionRequestValidator UpdateRegularExpenseDefinitionValidator = new();
    private static readonly DeleteRegularExpenseDefinitionRequestValidator DeleteRegularExpenseDefinitionValidator = new();
    private static readonly ReorderRegularExpenseDefinitionsRequestValidator ReorderRegularExpenseDefinitionsValidator = new();

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

    public async Task<MonthPlanPreparationDto> GetMonthPlanPreparationAsync(
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthExists = await dbContext.MonthPlans
            .AsNoTracking()
            .AnyAsync(x => x.Year == year && x.Month == month, cancellationToken);

        var sourceYear = year - 1;
        var sourceMonth = month;

        if (monthExists)
        {
            return new MonthPlanPreparationDto
            {
                Year = year,
                Month = month,
                MonthExists = true,
                SourceYear = sourceYear,
                SourceMonth = sourceMonth
            };
        }

        var sourceExpenses = await LoadMonthExpensesAsync(
            dbContext,
            sourceYear,
            sourceMonth,
            null,
            cancellationToken);
        var activeRecurringExpenseKeys = await LoadActiveRecurringExpenseKeysAsync(dbContext, cancellationToken);

        return new MonthPlanPreparationDto
        {
            Year = year,
            Month = month,
            MonthExists = false,
            SourceYear = sourceYear,
            SourceMonth = sourceMonth,
            Suggestions = sourceExpenses
                .Select(expense => BuildMonthPlanExpenseSuggestionDto(
                    expense,
                    sourceYear,
                    sourceMonth,
                    activeRecurringExpenseKeys))
                .ToList()
        };
    }

    public async Task<IReadOnlyList<RegularExpenseDefinitionDto>> GetRegularExpenseDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definitions = await dbContext.RegularExpenseDefinitions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .Select(x => x.MapRegularExpenseDefinitionToDto())
            .ToListAsync(cancellationToken);

        return definitions;
    }

    public async Task<RegularExpenseDefinitionDto> CreateRegularExpenseDefinitionAsync(
        CreateRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        CreateRegularExpenseDefinitionValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedTagId = await EnsureCategoryAndTagValidAsync(
            dbContext,
            request.CategoryId,
            request.TagId,
            cancellationToken);

        var definition = new RegularExpenseDefinition
        {
            Order = await dbContext.RegularExpenseDefinitions
                .Select(x => (int?)x.Order)
                .MaxAsync(cancellationToken) + 1 ?? 1,
            Name = request.Name,
            CategoryId = request.CategoryId,
            TagId = normalizedTagId,
            Amount = request.Amount,
            IsActive = true,
            ShowRemainingInUI = request.ShowRemainingInUI
        };

        dbContext.RegularExpenseDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildRegularExpenseDefinitionDtoAsync(dbContext, definition.Id, cancellationToken);
    }

    public async Task<RegularExpenseDefinitionDto> UpdateRegularExpenseDefinitionAsync(
        UpdateRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        UpdateRegularExpenseDefinitionValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definition = await dbContext.RegularExpenseDefinitions
                             .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException("Regular expense definition not found.");

        var normalizedTagId = await EnsureCategoryAndTagValidAsync(
            dbContext,
            request.CategoryId,
            request.TagId,
            cancellationToken);

        definition.Name = request.Name;
        definition.CategoryId = request.CategoryId;
        definition.TagId = normalizedTagId;
        definition.Amount = request.Amount;
        definition.IsActive = request.IsActive;
        definition.ShowRemainingInUI = request.ShowRemainingInUI;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildRegularExpenseDefinitionDtoAsync(dbContext, definition.Id, cancellationToken);
    }

    public async Task DeleteRegularExpenseDefinitionAsync(
        DeleteRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        DeleteRegularExpenseDefinitionValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definition = await dbContext.RegularExpenseDefinitions
                             .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException("Regular expense definition not found.");

        if (!definition.IsActive)
        {
            return;
        }

        definition.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRegularExpenseDefinitionPermanentlyAsync(
        DeleteRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        DeleteRegularExpenseDefinitionValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definition = await dbContext.RegularExpenseDefinitions
                             .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException("Regular expense definition not found.");

        dbContext.RegularExpenseDefinitions.Remove(definition);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderRegularExpenseDefinitionsAsync(
        ReorderRegularExpenseDefinitionsRequest request,
        CancellationToken cancellationToken)
    {
        ReorderRegularExpenseDefinitionsValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definitions = await dbContext.RegularExpenseDefinitions
            .Where(x => request.DefinitionIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (definitions.Count != request.DefinitionIds.Count)
        {
            throw new BadRequestException("Some regular expense definitions were not found.");
        }

        for (var i = 0; i < request.DefinitionIds.Count; i++)
        {
            var definition = definitions.First(x => x.Id == request.DefinitionIds[i]);
            definition.Order = i + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, year, month, cancellationToken);
        if (monthPlan.IsClosed)
        {
            return;
        }

        monthPlan.IsClosed = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        var nextMonth = new DateTime(year, month, 1).AddMonths(1);
        var nextMonthState = await GetOrCreateMonthPlanStateAsync(dbContext, nextMonth.Year, nextMonth.Month, cancellationToken);

        // Do not reopen or alter an already existing next month.
        if (!nextMonthState.WasCreated)
        {
            return;
        }

        await SyncRegularExpensesForMonthAsync(dbContext, nextMonthState.MonthPlan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await incomeService.SyncRegularIncomesForMonthAsync(nextMonth.Year, nextMonth.Month, cancellationToken);
        await loanService.SyncLoanInstallmentsForMonthAsync(nextMonth.Year, nextMonth.Month, cancellationToken);
    }

    public async Task OpenMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var monthPlanState = await GetOrCreateMonthPlanStateAsync(dbContext, year, month, cancellationToken);
        var monthPlan = monthPlanState.MonthPlan;

        if (!monthPlanState.WasCreated && !monthPlan.IsClosed)
        {
            return;
        }

        if (monthPlan.IsClosed)
        {
            monthPlan.IsClosed = false;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!monthPlanState.WasCreated)
        {
            return;
        }

        await SyncRegularExpensesForMonthAsync(dbContext, monthPlan, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await incomeService.SyncRegularIncomesForMonthAsync(year, month, cancellationToken);
        await loanService.SyncLoanInstallmentsForMonthAsync(year, month, cancellationToken);
    }

    public async Task<bool> AddRegularExpenseDefinitionToMonthAsync(int definitionId, int year, int month,
        CancellationToken cancellationToken)
    {
        if (definitionId <= 0)
        {
            throw new BadRequestException("Definition ID must be greater than 0.");
        }

        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var monthPlanState = await GetOrCreateMonthPlanStateAsync(dbContext, year, month, cancellationToken);
        var monthPlan = monthPlanState.MonthPlan;
        BudgetHelper.EnsureMonthIsOpen(monthPlan);

        var definition = await dbContext.RegularExpenseDefinitions
                             .AsNoTracking()
                             .FirstOrDefaultAsync(x => x.Id == definitionId, cancellationToken)
                         ?? throw new NotFoundException("Regular expense definition not found.");

        if (!definition.IsActive)
        {
            return false;
        }

        var existsInMonth = await dbContext.Expenses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == dbContext.CurrentBudgetOwnerUserId
                     && x.MonthPlanId == monthPlan.Id
                     && x.RegularExpenseDefinitionId == definitionId,
                cancellationToken);

        if (existsInMonth)
        {
            return false;
        }

        var nextOrder = await dbContext.Expenses
            .Where(x => x.MonthPlanId == monthPlan.Id)
            .Select(x => (int?)x.Order)
            .MaxAsync(cancellationToken) + 1 ?? 1;

        dbContext.Expenses.Add(new Expense
        {
            MonthPlanId = monthPlan.Id,
            Order = nextOrder,
            Name = definition.Name,
            CategoryId = definition.CategoryId,
            TagId = definition.TagId,
            RegularExpenseDefinitionId = definition.Id,
            PlannedAmount = definition.Amount,
            ActualAmount = 0,
            ShowRemainingInUI = definition.ShowRemainingInUI
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MonthPlanDto> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlanState = await GetOrCreateMonthPlanStateAsync(dbContext, year, month, cancellationToken);
        var monthPlan = monthPlanState.MonthPlan;

        if (monthPlanState.WasCreated && !monthPlan.IsClosed)
        {
            await SyncRegularExpensesForMonthAsync(dbContext, monthPlan, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await incomeService.SyncRegularIncomesForMonthAsync(year, month, cancellationToken);
            await loanService.SyncLoanInstallmentsForMonthAsync(year, month, cancellationToken);
        }

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

    public async Task<MonthlyFinancialPictureDto> GetMonthlyFinancialPictureAsync(
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var monthPlan = await GetMonthAsync(year, month, cancellationToken);
        var liveBalance = await incomeService.GetLiveBalanceAsync(year, month, cancellationToken);

        return new MonthlyFinancialPictureDto
        {
            MonthPlan = monthPlan,
            LiveBalance = liveBalance
        };
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        var monthPlan = await GetMonthAsync(year, month, cancellationToken);
        var today = dateTimeProvider.GetLocalDateOnly();
        var monthRelationToToday = CompareYearMonth(year, month, today.Year, today.Month);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var firstMonthInYear = await dbContext.MonthPlans
            .AsNoTracking()
            .Where(x => x.Year == year && x.Month <= month)
            .OrderBy(x => x.Month)
            .Select(x => (int?)x.Month)
            .FirstOrDefaultAsync(cancellationToken) ?? month;

        var incomesCount = monthRelationToToday switch
        {
            < 0 => await dbContext.Incomes
                .AsNoTracking()
                .CountAsync(x => x.MonthPlanId == monthPlan.Id && x.Amount > 0, cancellationToken),

            > 0 => 0,

            _ => await dbContext.Incomes
                .AsNoTracking()
                .CountAsync(
                    x => x.MonthPlanId == monthPlan.Id
                         && x.Amount > 0
                         && x.ExpectedDayOfMonth <= today,
                    cancellationToken)
        };

        var savingsTransfersCount = monthRelationToToday switch
        {
            < 0 => await dbContext.MonthSavingsTransferItems
                .AsNoTracking()
                .CountAsync(x => x.MonthPlanId == monthPlan.Id && x.Amount > 0, cancellationToken),

            > 0 => 0,

            _ => await dbContext.MonthSavingsTransferItems
                .AsNoTracking()
                .CountAsync(
                    x => x.MonthPlanId == monthPlan.Id
                         && x.Amount > 0
                         && x.TransferDate <= today,
                    cancellationToken)
        };

        var expensesByMonth = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.MonthPlan.Year == year
                        && x.MonthPlan.Month >= firstMonthInYear
                        && x.MonthPlan.Month <= month)
            .GroupBy(x => x.MonthPlan.Month)
            .Select(g => new
            {
                Month = g.Key,
                Planned = g.Sum(x => x.PlannedAmount),
                Spent = g.Sum(x => x.ActualAmount)
            })
            .ToListAsync(cancellationToken);

        var incomesByMonth = await dbContext.Incomes
            .AsNoTracking()
            .Where(x => x.MonthPlan.Year == year
                        && x.MonthPlan.Month >= firstMonthInYear
                        && x.MonthPlan.Month <= month)
            .GroupBy(x => x.MonthPlan.Month)
            .Select(g => new
            {
                Month = g.Key,
                Total = g.Sum(x => x.Amount)
            })
            .ToListAsync(cancellationToken);

        var expensesMap = expensesByMonth.ToDictionary(x => x.Month);
        var incomesMap = incomesByMonth.ToDictionary(x => x.Month, x => x.Total);

        var accountBalances = await dbContext.AccountMonthBalances
            .AsNoTracking()
            .Where(x => x.Year < year || (x.Year == year && x.Month <= month))
            .Select(x => new AccountBalanceSnapshot(
                x.AccountId,
                x.Account.Type,
                x.Account.ActiveFromUtc,
                x.Account.IsArchived,
                x.Account.ArchivedAtUtc,
                x.Account.UpdatedAtUtc,
                x.Year,
                x.Month,
                x.ClosingBalance))
            .ToListAsync(cancellationToken);

        var closedMonthKeys = await dbContext.MonthPlans
            .AsNoTracking()
            .Where(x => x.IsClosed)
            .Select(x => new { x.Year, x.Month })
            .ToListAsync(cancellationToken);
        var closedMonthKeySet = closedMonthKeys
            .Select(x => ToMonthKey(x.Year, x.Month))
            .ToHashSet();

        var timeline = new List<DashboardMonthlySavingsDto>(month - firstMonthInYear + 1);
        for (var i = firstMonthInYear; i <= month; i++)
        {
            expensesMap.TryGetValue(i, out var expenseData);
            incomesMap.TryGetValue(i, out var incomeAmount);

            var currentMonthDate = new DateTime(year, i, 1);
            var previousMonthDate = currentMonthDate.AddMonths(-1);

            var currentAccountsMoney = SumAccountsOverviewClosingBalances(
                accountBalances,
                year,
                i,
                closedMonthKeySet.Contains(ToMonthKey(year, i)));
            var previousAccountsMoney = SumAccountsOverviewClosingBalances(
                accountBalances,
                previousMonthDate.Year,
                previousMonthDate.Month,
                closedMonthKeySet.Contains(ToMonthKey(previousMonthDate.Year, previousMonthDate.Month)));

            var plannedAmount = expenseData?.Planned ?? 0m;
            var spentAmount = expenseData?.Spent ?? 0m;
            var savedAmount = currentAccountsMoney - previousAccountsMoney;

            timeline.Add(new DashboardMonthlySavingsDto
            {
                Year = year,
                Month = i,
                PlannedAmount = plannedAmount,
                SpentAmount = spentAmount,
                IncomeAmount = incomeAmount,
                SavedAmount = savedAmount
            });
        }

        var categoryRemaining = monthPlan.Expenses
            .GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(g => new DashboardCategoryRemainingDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                PlannedAmount = g.Where(x => x.PlannedAmount > 0).Sum(x => x.PlannedAmount),
                SpentAmount = g.Where(x => x.PlannedAmount > 0).Sum(x => x.ActualAmount),
                RemainingAmount = g.Sum(CalculateRemainingContribution)
            })
            .Where(x => x.RemainingAmount > 0)
            .OrderByDescending(x => x.RemainingAmount)
            .ToList();

        var monthlyCount = timeline.Count == 0 ? 1 : timeline.Count;
        var averageMonthlyIncome = timeline.Sum(x => x.IncomeAmount) / monthlyCount;
        var averageMonthlySpent = timeline.Sum(x => x.SpentAmount) / monthlyCount;
        var averageMonthlySaved = timeline.Sum(x => x.SavedAmount) / monthlyCount;
        var savedAmountThisMonth = timeline.FirstOrDefault(x => x.Month == month)?.SavedAmount ?? 0m;
        var expenseTransactionCount = monthPlan.Expenses
            .Sum(expense => expense.SupportsLineItems
                ? expense.LineItems.Count(x => x.Amount > 0 && IsDateReachedForCurrentMonth(x.OccurredAt, monthRelationToToday, today))
                : expense.ActualAmount > 0 ? 1 : 0);

        return new DashboardSummaryDto
        {
            Year = year,
            Month = month,
            TransactionCount = expenseTransactionCount + incomesCount + savingsTransfersCount,
            UnplannedSpentTotal = monthPlan.Expenses.Sum(x => CalculateOutsidePlanContribution(x.PlannedAmount, x.ActualAmount)),
            SavedAmountThisMonth = savedAmountThisMonth,
            SavedAmountYearToDate = timeline.Sum(x => x.SavedAmount),
            AverageMonthlyIncome = averageMonthlyIncome,
            AverageMonthlySpent = averageMonthlySpent,
            AverageMonthlySaved = averageMonthlySaved,
            CategoryRemainingItems = categoryRemaining,
            SavingsTimeline = timeline
        };
    }

    private static bool IsDateReachedForCurrentMonth(DateOnly date, int monthRelationToToday, DateOnly today)
    {
        return monthRelationToToday switch
        {
            < 0 => true,
            > 0 => false,
            _ => date <= today
        };
    }

    private static int CompareYearMonth(int leftYear, int leftMonth, int rightYear, int rightMonth)
    {
        if (leftYear != rightYear)
        {
            return leftYear.CompareTo(rightYear);
        }

        return leftMonth.CompareTo(rightMonth);
    }

    public async Task<YearStatisticsDto> GetYearStatisticsAsync(int year, CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 3000)
        {
            throw new BadRequestException("Year is out of allowed range.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var availableYears = await dbContext.MonthPlans
            .AsNoTracking()
            .Select(x => x.Year)
            .Concat(dbContext.AnnualPlans
                .AsNoTracking()
                .Select(x => x.Year))
            .Distinct()
            .ToListAsync(cancellationToken);

        var sortedAvailableYears = availableYears
            .OrderByDescending(x => x)
            .ToList();

        var expenseRows = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.MonthPlan.Year == year)
            .Select(x => new ExpenseYearSnapshot(
                x.Id,
                x.CategoryId,
                x.Category.Name,
                x.MonthPlan.Month,
                x.TagId,
                x.PlannedAmount,
                x.ActualAmount))
            .ToListAsync(cancellationToken);

        var populatedMonths = expenseRows
            .GroupBy(x => x.Month)
            .Where(group => group.Sum(x => x.ActualAmount) > 0)
            .Select(group => group.Key)
            .OrderBy(x => x)
            .ToList();

        var populatedMonthSet = populatedMonths.ToHashSet();

        var expenseLineItemRows = await dbContext.ExpenseLineItems
            .AsNoTracking()
            .Where(x => x.Expense.MonthPlan.Year == year)
            .Select(x => new ExpenseLineItemYearSnapshot(
                x.ExpenseId,
                x.Expense.CategoryId,
                x.Expense.MonthPlan.Month,
                x.Expense.TagId,
                x.TagId,
                x.Amount))
            .ToListAsync(cancellationToken);

        var categoryStatistics = expenseRows
            .GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(group =>
            {
                var monthlySpent = populatedMonths
                    .Select(monthNumber => group
                        .Where(x => x.Month == monthNumber)
                        .Sum(x => x.ActualAmount))
                    .ToList();

                var monthsCount = populatedMonths.Count == 0 ? 1 : populatedMonths.Count;
                var average = monthlySpent.Sum() / monthsCount;

                return new CategoryYearStatisticsDto
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.CategoryName,
                    TotalSpent = monthlySpent.Sum(),
                    AverageMonthlySpent = average,
                    MonthsWithExpenses = monthlySpent.Count(x => x > 0)
                };
            })
            .OrderByDescending(x => x.TotalSpent)
            .ThenBy(x => x.CategoryName)
            .ToList();

        var topCategories = categoryStatistics
            .Take(5)
            .ToList();

        var categoryBreakdown = expenseRows
            .GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(group =>
            {
                var monthlySpent = populatedMonths
                    .Select(monthNumber => group
                        .Where(x => x.Month == monthNumber)
                        .Sum(x => x.ActualAmount))
                    .ToList();

                return new YearCategoryBreakdownItemDto
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.CategoryName,
                    MonthlySpent = monthlySpent
                };
            })
            .OrderByDescending(x => x.MonthlySpent.Sum())
            .ThenBy(x => x.CategoryName)
            .ToList();

        var tags = await dbContext.Tags
            .AsNoTracking()
            .Select(x => new TagSnapshot(x.Id, x.CategoryId, x.ParentTagId, x.Name))
            .ToListAsync(cancellationToken);

        var tagsByCategory = tags
            .GroupBy(x => x.CategoryId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var expenseIdsWithLineItems = expenseLineItemRows
            .Select(x => x.ExpenseId)
            .Distinct()
            .ToHashSet();

        var expenseRowsWithoutLineItems = expenseRows
            .Where(x => !expenseIdsWithLineItems.Contains(x.ExpenseId))
            .ToList();

        var directTagMonthValues = new Dictionary<(int TagId, int Month), decimal>();

        foreach (var lineItem in expenseLineItemRows)
        {
            var effectiveTagId = lineItem.TagId ?? lineItem.ExpenseTagId;
            if (!effectiveTagId.HasValue)
            {
                continue;
            }

            var key = (effectiveTagId.Value, lineItem.Month);
            directTagMonthValues[key] = directTagMonthValues.GetValueOrDefault(key, 0m) + lineItem.Amount;
        }

        foreach (var expense in expenseRowsWithoutLineItems.Where(x => x.TagId.HasValue))
        {
            var key = (expense.TagId!.Value, expense.Month);
            directTagMonthValues[key] = directTagMonthValues.GetValueOrDefault(key, 0m) + expense.ActualAmount;
        }

        var categoryTagStatistics = new List<CategoryTagYearStatisticsDto>();
        foreach (var category in categoryStatistics)
        {
            tagsByCategory.TryGetValue(category.CategoryId, out var categoryTags);
            categoryTags ??= [];

            var descendantsByTag = categoryTags
                .ToDictionary(
                    x => x.Id,
                    x => GetDescendants(categoryTags, x.Id));

            foreach (var tag in categoryTags)
            {
                var descendants = descendantsByTag[tag.Id];
                var subtreeTagIds = new HashSet<int>(descendants) { tag.Id };

                var monthlySpent = populatedMonths
                    .Select(month => subtreeTagIds.Sum(tagId => directTagMonthValues.GetValueOrDefault((tagId, month), 0m)))
                    .ToList();

                var monthsCount = populatedMonths.Count == 0 ? 1 : populatedMonths.Count;
                var hasChildren = descendants.Count > 0;

                categoryTagStatistics.Add(new CategoryTagYearStatisticsDto
                {
                    CategoryId = category.CategoryId,
                    TagId = tag.Id,
                    ParentTagId = tag.ParentTagId,
                    TagName = tag.Name,
                    Depth = CalculateTagDepth(categoryTags, tag.Id),
                    HasChildren = hasChildren,
                    TotalSpent = monthlySpent.Sum(),
                    AverageMonthlySpent = monthlySpent.Sum() / monthsCount,
                    MonthsWithExpenses = monthlySpent.Count(x => x > 0)
                });
            }

            var untaggedMonthlySpent = populatedMonths
                .Select(month =>
                {
                    var lineItemsWithoutTag = expenseLineItemRows
                        .Where(x => x.CategoryId == category.CategoryId
                                    && x.Month == month
                                    && !x.TagId.HasValue
                                    && !x.ExpenseTagId.HasValue)
                        .Sum(x => x.Amount);

                    var expensesWithoutLineItemsAndTag = expenseRowsWithoutLineItems
                        .Where(x => x.CategoryId == category.CategoryId && x.Month == month && !x.TagId.HasValue)
                        .Sum(x => x.ActualAmount);

                    return lineItemsWithoutTag + expensesWithoutLineItemsAndTag;
                })
                .ToList();

            if (untaggedMonthlySpent.Any(x => x > 0))
            {
                var monthsCount = populatedMonths.Count == 0 ? 1 : populatedMonths.Count;
                categoryTagStatistics.Add(new CategoryTagYearStatisticsDto
                {
                    CategoryId = category.CategoryId,
                    TagId = null,
                    ParentTagId = null,
                    TagName = "(Bez tagu)",
                    Depth = 0,
                    HasChildren = false,
                    TotalSpent = untaggedMonthlySpent.Sum(),
                    AverageMonthlySpent = untaggedMonthlySpent.Sum() / monthsCount,
                    MonthsWithExpenses = untaggedMonthlySpent.Count(x => x > 0)
                });
            }
        }

        categoryTagStatistics = categoryTagStatistics
            .OrderBy(x => x.CategoryId)
            .ThenBy(x => x.Depth)
            .ThenByDescending(x => x.TotalSpent)
            .ThenBy(x => x.TagName)
            .ToList();

        var expensesByMonth = expenseRows
            .GroupBy(x => x.Month)
            .ToDictionary(
                x => x.Key,
                x => new
                {
                    PlannedAmount = x.Sum(item => item.PlannedAmount),
                    SpentAmount = x.Sum(item => item.ActualAmount),
                    UnplannedSpentAmount = x.Sum(item => CalculateOutsidePlanContribution(item.PlannedAmount, item.ActualAmount))
                });

        var incomesByMonth = await dbContext.Incomes
            .AsNoTracking()
            .Where(x => x.MonthPlan.Year == year)
            .GroupBy(x => x.MonthPlan.Month)
            .Select(group => new
            {
                Month = group.Key,
                Amount = group.Sum(x => x.Amount)
            })
            .ToDictionaryAsync(x => x.Month, x => x.Amount, cancellationToken);

        var savingsTransferredByMonth = await dbContext.MonthSavingsTransferItems
            .AsNoTracking()
            .Where(x => x.MonthPlan.Year == year)
            .GroupBy(x => x.MonthPlan.Month)
            .Select(group => new
            {
                Month = group.Key,
                Amount = group.Sum(x => x.Amount)
            })
            .ToDictionaryAsync(x => x.Month, x => x.Amount, cancellationToken);

        var accountBalanceSnapshots = await dbContext.AccountMonthBalances
            .AsNoTracking()
            .Where(x => x.Year <= year)
            .Select(x => new AccountBalanceSnapshot(
                x.AccountId,
                x.Account.Type,
                x.Account.ActiveFromUtc,
                x.Account.IsArchived,
                x.Account.ArchivedAtUtc,
                x.Account.UpdatedAtUtc,
                x.Year,
                x.Month,
                x.ClosingBalance))
            .ToListAsync(cancellationToken);

        var closedMonthKeys = await dbContext.MonthPlans
            .AsNoTracking()
            .Where(x => x.IsClosed)
            .Select(x => new { x.Year, x.Month })
            .ToListAsync(cancellationToken);
        var closedMonthKeySet = closedMonthKeys
            .Select(x => ToMonthKey(x.Year, x.Month))
            .ToHashSet();

        var monthlySavedAmounts = new Dictionary<int, decimal>(populatedMonths.Count);
        foreach (var monthNumber in populatedMonths)
        {
            var currentMonthDate = new DateTime(year, monthNumber, 1);
            var previousMonthDate = currentMonthDate.AddMonths(-1);

            var currentAccountsMoney = SumAccountsOverviewClosingBalances(
                accountBalanceSnapshots,
                year,
                monthNumber,
                closedMonthKeySet.Contains(ToMonthKey(year, monthNumber)));
            var previousAccountsMoney = SumAccountsOverviewClosingBalances(
                accountBalanceSnapshots,
                previousMonthDate.Year,
                previousMonthDate.Month,
                closedMonthKeySet.Contains(ToMonthKey(previousMonthDate.Year, previousMonthDate.Month)));

            monthlySavedAmounts[monthNumber] = currentAccountsMoney - previousAccountsMoney;
        }

        var monthlyFinance = populatedMonths
            .Select(monthNumber =>
            {
                expensesByMonth.TryGetValue(monthNumber, out var expenseData);
                incomesByMonth.TryGetValue(monthNumber, out var incomeAmount);
                savingsTransferredByMonth.TryGetValue(monthNumber, out var savingsTransferredAmount);
                monthlySavedAmounts.TryGetValue(monthNumber, out var savedAmount);

                return new YearMonthlyFinanceDto
                {
                    Month = monthNumber,
                    IncomeAmount = incomeAmount,
                    PlannedAmount = expenseData?.PlannedAmount ?? 0m,
                    SpentAmount = expenseData?.SpentAmount ?? 0m,
                    UnplannedSpentAmount = expenseData?.UnplannedSpentAmount ?? 0m,
                    SavingsTransferredAmount = savingsTransferredAmount,
                    SavedAmount = savedAmount
                };
            })
            .ToList();

        var activeAccountIds = await dbContext.AccountMonthBalances
            .AsNoTracking()
            .Where(x => x.Year == year)
            .Select(x => x.AccountId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var accountSnapshots = await dbContext.Accounts
            .AsNoTracking()
            .Where(x => activeAccountIds.Contains(x.Id))
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.ActiveFromUtc,
                x.IsArchived,
                x.ArchivedAtUtc,
                x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var accountBalanceMonths = await dbContext.AccountMonthBalances
            .AsNoTracking()
            .Where(x => x.Year == year)
            .Where(x => populatedMonthSet.Contains(x.Month))
            .Select(x => x.Month)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var accountBalances = accountSnapshots
            .Select(account =>
            {
                var monthlyBalances = accountBalanceMonths
                    .Select(monthNumber => GetClosingBalanceForApplicableAccountInMonth(
                        accountBalanceSnapshots,
                        account.Id,
                        account.ActiveFromUtc,
                        account.IsArchived,
                        account.ArchivedAtUtc,
                        account.UpdatedAtUtc,
                        year,
                        monthNumber))
                    .ToList();

                return new AccountYearBalanceDto
                {
                    AccountId = account.Id,
                    AccountName = account.Name,
                    MonthlyClosingBalances = monthlyBalances
                };
            })
            .Where(x => x.MonthlyClosingBalances.Any(balance => balance.HasValue))
            .ToList();

        var deviationAlertCandidates = BuildDeviationAlertCandidates(expenseRows, populatedMonths, year);

        var annualPlan = await dbContext.AnnualPlans
            .AsNoTracking()
            .Where(x => x.Year == year)
            .Select(x => new AnnualPlanDto
            {
                Year = x.Year,
                ExpectedIncomeAmount = x.ExpectedIncomeAmount,
                ExpectedSavingsAmount = x.ExpectedSavingsAmount
            })
            .SingleOrDefaultAsync(cancellationToken) ?? new AnnualPlanDto
            {
                Year = year
            };

        return new YearStatisticsDto
        {
            Year = year,
            AvailableYears = sortedAvailableYears,
            PopulatedMonths = populatedMonths,
            AccountBalanceMonths = accountBalanceMonths,
            CategoryStatistics = categoryStatistics,
            TopCategories = topCategories,
            CategoryTagStatistics = categoryTagStatistics,
            CategoryBreakdown = categoryBreakdown,
            MonthlyFinance = monthlyFinance,
            AccountBalances = accountBalances,
            DeviationAlertCandidates = deviationAlertCandidates,
            AnnualPlan = annualPlan
        };
    }

    public async Task<AnnualPlanDto> UpsertAnnualPlanAsync(
        UpsertAnnualPlanRequest request,
        CancellationToken cancellationToken)
    {
        UpsertAnnualPlanValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var annualPlan = await dbContext.AnnualPlans
            .SingleOrDefaultAsync(x => x.Year == request.Year, cancellationToken);

        if (annualPlan is null)
        {
            annualPlan = new AnnualPlan
            {
                Year = request.Year,
                ExpectedIncomeAmount = request.ExpectedIncomeAmount,
                ExpectedSavingsAmount = request.ExpectedSavingsAmount
            };
            dbContext.AnnualPlans.Add(annualPlan);
        }
        else
        {
            annualPlan.ExpectedIncomeAmount = request.ExpectedIncomeAmount;
            annualPlan.ExpectedSavingsAmount = request.ExpectedSavingsAmount;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AnnualPlanDto
        {
            Year = annualPlan.Year,
            ExpectedIncomeAmount = annualPlan.ExpectedIncomeAmount,
            ExpectedSavingsAmount = annualPlan.ExpectedSavingsAmount
        };
    }

    private static IReadOnlyList<CategoryDeviationAlertCandidateDto> BuildDeviationAlertCandidates(
        IReadOnlyList<ExpenseYearSnapshot> expenseRows,
        IReadOnlyList<int> populatedMonths,
        int year)
    {
        if (expenseRows.Count == 0 || populatedMonths.Count == 0)
        {
            return [];
        }

        var monthlyCategorySpent = expenseRows
            .GroupBy(x => new { x.CategoryId, x.CategoryName, x.Month })
            .ToDictionary(
                group => (group.Key.CategoryId, group.Key.CategoryName, group.Key.Month),
                group => group.Sum(x => x.ActualAmount));

        var categories = expenseRows
            .GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(group => (group.Key.CategoryId, group.Key.CategoryName))
            .OrderBy(x => x.CategoryName)
            .ToList();

        var candidates = new List<CategoryDeviationAlertCandidateDto>();

        foreach (var (categoryId, categoryName) in categories)
        {
            var categoryMonthlyValues = populatedMonths
                .Select(month => monthlyCategorySpent.GetValueOrDefault((categoryId, categoryName, month), 0m))
                .ToList();

            for (var index = 0; index < populatedMonths.Count; index++)
            {
                var currentSpent = categoryMonthlyValues[index];
                if (currentSpent <= 0m)
                {
                    continue;
                }

                var priorValues = categoryMonthlyValues
                    .Take(index)
                    .Where(value => value > 0m)
                    .ToList();

                if (priorValues.Count == 0)
                {
                    continue;
                }

                var historicalAverage = priorValues.Average();
                if (historicalAverage <= 0m)
                {
                    continue;
                }

                var deviationPercent = ((currentSpent - historicalAverage) / historicalAverage) * 100m;
                if (deviationPercent <= 20m)
                {
                    continue;
                }

                candidates.Add(new CategoryDeviationAlertCandidateDto
                {
                    Year = year,
                    Month = populatedMonths[index],
                    CategoryId = categoryId,
                    CategoryName = categoryName,
                    CurrentSpentAmount = currentSpent,
                    HistoricalAverageAmount = historicalAverage,
                    DeviationPercent = deviationPercent,
                    ThresholdPercent = 20m
                });
            }
        }

        return candidates
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ThenByDescending(x => x.DeviationPercent)
            .ThenBy(x => x.CategoryName)
            .ToList();
    }

    public async Task<IReadOnlyList<ExpenseHistorySearchResultDto>> SearchExpenseHistoryAsync(
        SearchExpenseHistoryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value > request.ToDate.Value)
        {
            throw new BadRequestException("Zakres dat jest nieprawidłowy.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tags = await dbContext.Tags
            .AsNoTracking()
            .Select(x => new TagSnapshot(x.Id, x.CategoryId, x.ParentTagId, x.Name))
            .ToListAsync(cancellationToken);

        var tagById = tags.ToDictionary(x => x.Id);
        var normalizedQuery = request.Query?.Trim();
        var hasQuery = !string.IsNullOrWhiteSpace(normalizedQuery);
        var maxResults = request.MaxResults <= 0 ? 200 : Math.Min(request.MaxResults, 1000);

        var expenseQuery = dbContext.Expenses
            .AsNoTracking()
            .Where(x => !request.CategoryId.HasValue || x.CategoryId == request.CategoryId.Value);

        if (request.FromDate.HasValue)
        {
            var fromDate = request.FromDate.Value;
            expenseQuery = expenseQuery
                .Where(x => x.MonthPlan.Year > fromDate.Year
                            || (x.MonthPlan.Year == fromDate.Year && x.MonthPlan.Month >= fromDate.Month));
        }

        if (request.ToDate.HasValue)
        {
            var toDate = request.ToDate.Value;
            expenseQuery = expenseQuery
                .Where(x => x.MonthPlan.Year < toDate.Year
                            || (x.MonthPlan.Year == toDate.Year && x.MonthPlan.Month <= toDate.Month));
        }

        var expenseRows = await expenseQuery
            .OrderByDescending(x => x.MonthPlan.Year)
            .ThenByDescending(x => x.MonthPlan.Month)
            .ThenByDescending(x => x.Id)
            .Select(x => new ExpenseHistorySearchSnapshot(
                x.Id,
                x.Name,
                x.MonthPlan.Year,
                x.MonthPlan.Month,
                x.CategoryId,
                x.Category.Name,
                x.TagId,
                x.PlannedAmount,
                x.ActualAmount,
                x.LineItems
                    .Select(lineItem => new ExpenseLineItemSearchSnapshot(
                        lineItem.Description,
                        lineItem.TagId,
                        lineItem.Amount))
                    .ToList()))
            .ToListAsync(cancellationToken);

        var results = new List<ExpenseHistorySearchResultDto>(Math.Min(maxResults, expenseRows.Count));

        foreach (var expense in expenseRows)
        {
            var expenseTagHierarchy = ResolveRootAndSubTag(expense.TagId, tagById);
            var matchesTagFilter = MatchesTagFilter(expenseTagHierarchy, request);
            TagHierarchySnapshot? matchingLineItemTagHierarchy = null;
            string? matchingDescription = null;
            decimal? matchingAmount = null;
            foreach (var lineItem in expense.LineItems)
            {
                var lineItemEffectiveTagId = lineItem.TagId ?? expense.TagId;
                var lineItemTagHierarchy = ResolveRootAndSubTag(lineItemEffectiveTagId, tagById);

                if (!matchesTagFilter && MatchesTagFilter(lineItemTagHierarchy, request))
                {
                    matchesTagFilter = true;
                    matchingDescription = lineItem.Description;
                    matchingAmount = lineItem.Amount;
                    matchingLineItemTagHierarchy = lineItemTagHierarchy;
                }

                if (hasQuery
                    && matchingDescription is null
                    && !string.IsNullOrWhiteSpace(lineItem.Description)
                    && lineItem.Description.Contains(normalizedQuery!, StringComparison.CurrentCultureIgnoreCase))
                {
                    matchingDescription = lineItem.Description;
                    matchingAmount = lineItem.Amount;
                    matchingLineItemTagHierarchy = lineItemTagHierarchy;
                }
            }

            if (!matchesTagFilter)
            {
                continue;
            }

            var matchesName = !hasQuery || expense.Name.Contains(normalizedQuery!, StringComparison.CurrentCultureIgnoreCase);
            if (hasQuery && !matchesName && matchingDescription is null)
            {
                continue;
            }

            var displayHierarchy = matchingLineItemTagHierarchy ?? expenseTagHierarchy;
            if (!displayHierarchy.RootTagId.HasValue)
            {
                var fallbackEffectiveTagId = expense.LineItems
                    .Select(x => x.TagId ?? expense.TagId)
                    .FirstOrDefault(x => x.HasValue);

                displayHierarchy = ResolveRootAndSubTag(fallbackEffectiveTagId, tagById);
            }

            results.Add(new ExpenseHistorySearchResultDto
            {
                ExpenseId = expense.ExpenseId,
                Year = expense.Year,
                Month = expense.Month,
                ExpenseName = expense.Name,
                CategoryId = expense.CategoryId,
                CategoryName = expense.CategoryName,
                RootTagId = displayHierarchy.RootTagId,
                RootTagName = displayHierarchy.RootTagName,
                SubTagId = displayHierarchy.SubTagId,
                SubTagName = displayHierarchy.SubTagName,
                PlannedAmount = expense.PlannedAmount,
                ActualAmount = matchingAmount ?? expense.ActualAmount,
                MatchingDescription = matchingDescription
            });

            if (results.Count >= maxResults)
            {
                break;
            }
        }

        return results;
    }

    public async Task<CategoryRangeStatisticsDto> GetCategoryRangeStatisticsAsync(
        IReadOnlyList<int>? categoryIds,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            throw new BadRequestException("Date range is invalid.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var minMonthPlan = await dbContext.MonthPlans
            .AsNoTracking()
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => new { x.Year, x.Month })
            .FirstOrDefaultAsync(cancellationToken);

        var maxMonthPlan = await dbContext.MonthPlans
            .AsNoTracking()
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => new { x.Year, x.Month })
            .FirstOrDefaultAsync(cancellationToken);

        if (minMonthPlan is null || maxMonthPlan is null)
        {
            return new CategoryRangeStatisticsDto();
        }

        var rangeFrom = fromDate ?? new DateOnly(minMonthPlan.Year, minMonthPlan.Month, 1);
        var rangeTo = toDate ?? new DateOnly(maxMonthPlan.Year, maxMonthPlan.Month, 1);

        if (rangeFrom > rangeTo)
        {
            throw new BadRequestException("Date range is invalid.");
        }

        var rangeMonthCount = ((rangeTo.Year - rangeFrom.Year) * 12) + rangeTo.Month - rangeFrom.Month + 1;
        rangeMonthCount = Math.Max(1, rangeMonthCount);

        var monthRange = Enumerable
            .Range(0, rangeMonthCount)
            .Select(offset => rangeFrom.AddMonths(offset))
            .ToList();

        var expenseQuery = dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.MonthPlan.Year > rangeFrom.Year
                        || (x.MonthPlan.Year == rangeFrom.Year && x.MonthPlan.Month >= rangeFrom.Month))
            .Where(x => x.MonthPlan.Year < rangeTo.Year
                        || (x.MonthPlan.Year == rangeTo.Year && x.MonthPlan.Month <= rangeTo.Month));

        if (categoryIds is { Count: > 0 })
        {
            expenseQuery = expenseQuery.Where(x => categoryIds.Contains(x.CategoryId));
        }

        var expenseRows = await expenseQuery
            .Select(x => new
            {
                x.Id,
                x.CategoryId,
                CategoryName = x.Category.Name,
                Year = x.MonthPlan.Year,
                Month = x.MonthPlan.Month,
                x.TagId,
                x.ActualAmount
            })
            .ToListAsync(cancellationToken);

        var expenseLineItemQuery = dbContext.ExpenseLineItems
            .AsNoTracking()
            .Where(x => x.Expense.MonthPlan.Year > rangeFrom.Year
                        || (x.Expense.MonthPlan.Year == rangeFrom.Year && x.Expense.MonthPlan.Month >= rangeFrom.Month))
            .Where(x => x.Expense.MonthPlan.Year < rangeTo.Year
                        || (x.Expense.MonthPlan.Year == rangeTo.Year && x.Expense.MonthPlan.Month <= rangeTo.Month));

        if (categoryIds is { Count: > 0 })
        {
            expenseLineItemQuery = expenseLineItemQuery.Where(x => categoryIds.Contains(x.Expense.CategoryId));
        }

        var expenseLineItemRows = await expenseLineItemQuery
            .Select(x => new
            {
                x.ExpenseId,
                CategoryId = x.Expense.CategoryId,
                Year = x.Expense.MonthPlan.Year,
                Month = x.Expense.MonthPlan.Month,
                ExpenseTagId = x.Expense.TagId,
                x.TagId,
                x.Amount
            })
            .ToListAsync(cancellationToken);

        var categoryStatistics = expenseRows
            .GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(group =>
            {
                var total = group.Sum(x => x.ActualAmount);
                var monthsWithExpenses = group
                    .GroupBy(x => new { x.Year, x.Month })
                    .Count(x => x.Sum(v => v.ActualAmount) > 0);

                return new CategoryYearStatisticsDto
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.CategoryName,
                    TotalSpent = total,
                    AverageMonthlySpent = total / rangeMonthCount,
                    MonthsWithExpenses = monthsWithExpenses
                };
            })
            .OrderByDescending(x => x.TotalSpent)
            .ThenBy(x => x.CategoryName)
            .ToList();

        var categoryIdSet = categoryStatistics.Select(x => x.CategoryId).ToHashSet();
        var tags = await dbContext.Tags
            .AsNoTracking()
            .Where(x => categoryIdSet.Contains(x.CategoryId))
            .Select(x => new TagSnapshot(x.Id, x.CategoryId, x.ParentTagId, x.Name))
            .ToListAsync(cancellationToken);

        var tagsByCategory = tags
            .GroupBy(x => x.CategoryId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var expenseIdsWithLineItems = expenseLineItemRows
            .Select(x => x.ExpenseId)
            .Distinct()
            .ToHashSet();

        var expenseRowsWithoutLineItems = expenseRows
            .Where(x => !expenseIdsWithLineItems.Contains(x.Id))
            .ToList();

        var directTagMonthValues = new Dictionary<(int TagId, int Year, int Month), decimal>();

        foreach (var lineItem in expenseLineItemRows)
        {
            var effectiveTagId = lineItem.TagId ?? lineItem.ExpenseTagId;
            if (!effectiveTagId.HasValue)
            {
                continue;
            }

            var key = (effectiveTagId.Value, lineItem.Year, lineItem.Month);
            directTagMonthValues[key] = directTagMonthValues.GetValueOrDefault(key, 0m) + lineItem.Amount;
        }

        foreach (var expense in expenseRowsWithoutLineItems.Where(x => x.TagId.HasValue))
        {
            var key = (expense.TagId!.Value, expense.Year, expense.Month);
            directTagMonthValues[key] = directTagMonthValues.GetValueOrDefault(key, 0m) + expense.ActualAmount;
        }

        var monthKeysInRange = monthRange
            .Select(x => (x.Year, x.Month))
            .ToList();

        var categoryTagStatistics = new List<CategoryTagYearStatisticsDto>();
        foreach (var category in categoryStatistics)
        {
            tagsByCategory.TryGetValue(category.CategoryId, out var categoryTags);
            categoryTags ??= [];

            var descendantsByTag = categoryTags
                .ToDictionary(
                    x => x.Id,
                    x => GetDescendants(categoryTags, x.Id));

            foreach (var tag in categoryTags)
            {
                var descendants = descendantsByTag[tag.Id];
                var subtreeTagIds = new HashSet<int>(descendants) { tag.Id };

                var monthlySpent = monthKeysInRange
                    .Select(monthKey => subtreeTagIds.Sum(tagId => directTagMonthValues.GetValueOrDefault((tagId, monthKey.Year, monthKey.Month), 0m)))
                    .ToList();

                var total = monthlySpent.Sum();
                var hasChildren = descendants.Count > 0;

                categoryTagStatistics.Add(new CategoryTagYearStatisticsDto
                {
                    CategoryId = category.CategoryId,
                    TagId = tag.Id,
                    ParentTagId = tag.ParentTagId,
                    TagName = tag.Name,
                    Depth = CalculateTagDepth(categoryTags, tag.Id),
                    HasChildren = hasChildren,
                    TotalSpent = total,
                    AverageMonthlySpent = total / rangeMonthCount,
                    MonthsWithExpenses = monthlySpent.Count(x => x > 0)
                });
            }

            var untaggedMonthlySpent = monthKeysInRange
                .Select(monthKey =>
                {
                    var lineItemsWithoutTag = expenseLineItemRows
                        .Where(x => x.CategoryId == category.CategoryId
                                    && x.Year == monthKey.Year
                                    && x.Month == monthKey.Month
                                    && !x.TagId.HasValue
                                    && !x.ExpenseTagId.HasValue)
                        .Sum(x => x.Amount);

                    var expensesWithoutLineItemsAndTag = expenseRowsWithoutLineItems
                        .Where(x => x.CategoryId == category.CategoryId
                                    && x.Year == monthKey.Year
                                    && x.Month == monthKey.Month
                                    && !x.TagId.HasValue)
                        .Sum(x => x.ActualAmount);

                    return lineItemsWithoutTag + expensesWithoutLineItemsAndTag;
                })
                .ToList();

            if (untaggedMonthlySpent.Any(x => x > 0))
            {
                var untaggedTotal = untaggedMonthlySpent.Sum();
                categoryTagStatistics.Add(new CategoryTagYearStatisticsDto
                {
                    CategoryId = category.CategoryId,
                    TagId = null,
                    ParentTagId = null,
                    TagName = "(Bez tagu)",
                    Depth = 0,
                    HasChildren = false,
                    TotalSpent = untaggedTotal,
                    AverageMonthlySpent = untaggedTotal / rangeMonthCount,
                    MonthsWithExpenses = untaggedMonthlySpent.Count(x => x > 0)
                });
            }
        }

        categoryTagStatistics = categoryTagStatistics
            .OrderBy(x => x.CategoryId)
            .ThenBy(x => x.Depth)
            .ThenByDescending(x => x.TotalSpent)
            .ThenBy(x => x.TagName)
            .ToList();

        return new CategoryRangeStatisticsDto
        {
            CategoryStatistics = categoryStatistics,
            CategoryTagStatistics = categoryTagStatistics,
            RangeMonthCount = rangeMonthCount,
            FirstYear = rangeFrom.Year,
            LastYear = rangeTo.Year
        };
    }

    public async Task<IReadOnlyList<TagUsageCountDto>> GetTagUsageCountsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expenseTagCounts = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.TagId.HasValue)
            .GroupBy(x => x.TagId!.Value)
            .Select(x => new TagUsageCountDto
            {
                TagId = x.Key,
                UsageCount = x.Count()
            })
            .ToListAsync(cancellationToken);

        var lineItemTagCounts = await dbContext.ExpenseLineItems
            .AsNoTracking()
            .Where(x => x.TagId.HasValue)
            .GroupBy(x => x.TagId!.Value)
            .Select(x => new TagUsageCountDto
            {
                TagId = x.Key,
                UsageCount = x.Count()
            })
            .ToListAsync(cancellationToken);

        var merged = expenseTagCounts
            .Concat(lineItemTagCounts)
            .GroupBy(x => x.TagId)
            .Select(x => new TagUsageCountDto
            {
                TagId = x.Key,
                UsageCount = x.Sum(v => v.UsageCount)
            })
            .OrderByDescending(x => x.UsageCount)
            .ThenBy(x => x.TagId)
            .ToList();

        return merged;
    }

    public async Task<IReadOnlyList<CategoryLifetimeExpenseTotalDto>> GetCategoryLifetimeExpenseTotalsAsync(
        IReadOnlyList<int>? categoryIds,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            throw new BadRequestException("Date range is invalid.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.Expenses.AsNoTracking();

        if (fromDate.HasValue)
        {
            var fromYear = fromDate.Value.Year;
            var fromMonth = fromDate.Value.Month;

            query = query.Where(x => x.MonthPlan.Year > fromYear
                                     || (x.MonthPlan.Year == fromYear && x.MonthPlan.Month >= fromMonth));
        }

        if (toDate.HasValue)
        {
            var toYear = toDate.Value.Year;
            var toMonth = toDate.Value.Month;

            query = query.Where(x => x.MonthPlan.Year < toYear
                                     || (x.MonthPlan.Year == toYear && x.MonthPlan.Month <= toMonth));
        }

        if (categoryIds is { Count: > 0 })
        {
            query = query.Where(x => categoryIds.Contains(x.CategoryId));
        }

        var totals = await query
            .GroupBy(x => new { x.CategoryId, x.Category.Name })
            .Select(group => new CategoryLifetimeExpenseTotalDto
            {
                CategoryId = group.Key.CategoryId,
                CategoryName = group.Key.Name,
                TotalSpent = group.Sum(x => x.ActualAmount),
                FirstYear = group.Min(x => (int?)x.MonthPlan.Year),
                LastYear = group.Max(x => (int?)x.MonthPlan.Year)
            })
            .OrderByDescending(x => x.TotalSpent)
            .ThenBy(x => x.CategoryName)
            .ToListAsync(cancellationToken);

        return totals;
    }

    public async Task<MonthSavingsTransferItemDto> CreateMonthSavingsTransferItemAsync(
        CreateMonthSavingsTransferItemRequest request, CancellationToken cancellationToken)
    {
        CreateSavingsTransferItemValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, request.Year, request.Month, cancellationToken);
        BudgetHelper.EnsureMonthIsOpen(monthPlan);

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

        BudgetHelper.EnsureMonthIsOpen(item.MonthPlan);

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
                       .Include(x => x.MonthPlan)
                       .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                   ?? throw new NotFoundException("Savings transfer item not found.");

        BudgetHelper.EnsureMonthIsOpen(item.MonthPlan);

        dbContext.MonthSavingsTransferItems.Remove(item);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        CreateExpenseValidator.ValidateOrThrowBadRequest(request);

        var normalizedName = request.Name;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, request.Year, request.Month, cancellationToken);
        BudgetHelper.EnsureMonthIsOpen(monthPlan);
        var envelopeUsageBefore = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            monthPlan.Id,
            request.CategoryId,
            cancellationToken);

        var category = await dbContext.Categories
                           .AsNoTracking()
                           .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken)
                       ?? throw new NotFoundException("Category not found.");

        var normalizedTagId = await EnsureCategoryAndTagValidAsync(
            dbContext,
            request.CategoryId,
            request.TagId,
            cancellationToken);
        var normalizedTagName = normalizedTagId.HasValue
            ? await dbContext.Tags
                .AsNoTracking()
                .Where(x => x.Id == normalizedTagId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var expense = new Expense
        {
            MonthPlanId = monthPlan.Id,
            Order = await dbContext.Expenses
                .Where(x => x.MonthPlanId == monthPlan.Id)
                .Select(x => (int?)x.Order)
                .MaxAsync(cancellationToken) + 1 ?? 1,
            Name = normalizedName,
            CategoryId = request.CategoryId,
            TagId = normalizedTagId,
            PlannedAmount = request.PlannedAmount,
            ActualAmount = request.ActualAmount,
            ShowRemainingInUI = request.ShowRemainingInUI
        };

        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken);

        var envelopeUsageAfter = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            monthPlan.Id,
            request.CategoryId,
            cancellationToken);

        await EmitBudgetExceededEventIfNeededAsync(
            envelopeUsageBefore,
            envelopeUsageAfter,
            monthPlan.Year,
            monthPlan.Month,
            cancellationToken);

        return await BuildExpenseDtoAsync(dbContext, expense.Id, cancellationToken, category.Name, normalizedTagName);
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

        var monthPlan = await dbContext.MonthPlans
            .FirstOrDefaultAsync(x => x.Id == monthPlanIds[0], cancellationToken)
            ?? throw new NotFoundException("Month plan not found.");
        BudgetHelper.EnsureMonthIsOpen(monthPlan);

        for (var i = 0; i < request.ExpenseIds.Count; i++)
        {
            var expense = expenses.First(x => x.Id == request.ExpenseIds[i]);
            expense.Order = i + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ApplyMonthPlanSuggestionsAsync(
        ApplyMonthPlanSuggestionsRequest request,
        CancellationToken cancellationToken)
    {
        ApplyMonthPlanSuggestionsValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var sourceYear = request.Year - 1;
        var sourceMonth = request.Month;
        var selectedIds = request.Suggestions.Select(x => x.SourceExpenseId).ToArray();

        var sourceExpenses = await LoadMonthExpensesAsync(
            dbContext,
            sourceYear,
            sourceMonth,
            selectedIds,
            cancellationToken);

        if (sourceExpenses.Count != request.Suggestions.Count)
        {
            throw new BadRequestException("Some suggestion source expenses were not found in source month.");
        }

        var targetMonthState = await GetOrCreateMonthPlanStateAsync(dbContext, request.Year, request.Month, cancellationToken);
        var targetMonthPlan = targetMonthState.MonthPlan;
        BudgetHelper.EnsureMonthIsOpen(targetMonthPlan);

        if (targetMonthState.WasCreated)
        {
            await SyncRegularExpensesForMonthAsync(dbContext, targetMonthPlan, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await incomeService.SyncRegularIncomesForMonthAsync(request.Year, request.Month, cancellationToken);
            await loanService.SyncLoanInstallmentsForMonthAsync(request.Year, request.Month, cancellationToken);
        }

        var selectedItemsBySourceId = request.Suggestions.ToDictionary(x => x.SourceExpenseId);
        var existingRegularDefinitionIdsInTarget = await dbContext.Expenses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.UserId == dbContext.CurrentBudgetOwnerUserId
                        && x.MonthPlanId == targetMonthPlan.Id
                        && x.RegularExpenseDefinitionId.HasValue)
            .Select(x => x.RegularExpenseDefinitionId!.Value)
            .ToListAsync(cancellationToken);

        var existingRegularDefinitionIdsSet = existingRegularDefinitionIdsInTarget.ToHashSet();
        var activeRecurringExpenseKeys = await LoadActiveRecurringExpenseKeysAsync(dbContext, cancellationToken);
        var maxOrder = await dbContext.Expenses
            .Where(x => x.MonthPlanId == targetMonthPlan.Id)
            .Select(x => (int?)x.Order)
            .MaxAsync(cancellationToken) ?? 0;

        var createdCount = 0;
        foreach (var sourceExpense in sourceExpenses)
        {
            var selectedItem = selectedItemsBySourceId[sourceExpense.Id];
            if (!IsHistoricalSuggestionAvailable(sourceExpense, activeRecurringExpenseKeys))
            {
                continue;
            }

            if (sourceExpense.RegularExpenseDefinitionId.HasValue
                && existingRegularDefinitionIdsSet.Contains(sourceExpense.RegularExpenseDefinitionId.Value))
            {
                continue;
            }

            maxOrder++;
            dbContext.Expenses.Add(new Expense
            {
                MonthPlanId = targetMonthPlan.Id,
                Order = maxOrder,
                Name = sourceExpense.Name,
                CategoryId = sourceExpense.CategoryId,
                TagId = sourceExpense.TagId,
                PlannedAmount = selectedItem.PlannedAmount,
                ActualAmount = 0,
                ShowRemainingInUI = sourceExpense.ShowRemainingInUI
            });

            createdCount++;
        }

        if (createdCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return createdCount;
    }

    private static bool MatchesTagFilter(TagHierarchySnapshot tagHierarchy, SearchExpenseHistoryRequest request)
    {
        var matchesRootTag = !request.RootTagId.HasValue || tagHierarchy.RootTagId == request.RootTagId.Value;
        var matchesSubTag = !request.SubTagId.HasValue || tagHierarchy.SubTagId == request.SubTagId.Value;

        return matchesRootTag && matchesSubTag;
    }

    public async Task<int> CopySelectedExpensesToMonthAsync(
        CopySelectedExpensesToMonthRequest request,
        CancellationToken cancellationToken)
    {
        CopySelectedExpensesToMonthValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (!await dbContext.MonthPlans
                .AsNoTracking()
                .AnyAsync(x => x.Year == request.Year && x.Month == request.Month, cancellationToken))
        {
            throw new NotFoundException("Source month plan not found.");
        }

        var sourceExpenses = await LoadMonthExpensesAsync(
            dbContext,
            request.Year,
            request.Month,
            request.ExpenseIds,
            cancellationToken);

        if (sourceExpenses.Count != request.ExpenseIds.Count)
        {
            throw new BadRequestException("Some expenses were not found in selected month.");
        }

        var targetMonthState = await GetOrCreateMonthPlanStateAsync(
            dbContext,
            request.TargetYear,
            request.TargetMonth,
            cancellationToken);
        var targetMonthPlan = targetMonthState.MonthPlan;
        BudgetHelper.EnsureMonthIsOpen(targetMonthPlan);

        if (targetMonthState.WasCreated)
        {
            await SyncRegularExpensesForMonthAsync(dbContext, targetMonthPlan, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await incomeService.SyncRegularIncomesForMonthAsync(request.TargetYear, request.TargetMonth, cancellationToken);
            await loanService.SyncLoanInstallmentsForMonthAsync(request.TargetYear, request.TargetMonth, cancellationToken);
        }

        var existingRegularDefinitionIdsInTarget = await dbContext.Expenses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.UserId == dbContext.CurrentBudgetOwnerUserId
                        && x.MonthPlanId == targetMonthPlan.Id
                        && x.RegularExpenseDefinitionId.HasValue)
            .Select(x => x.RegularExpenseDefinitionId!.Value)
            .ToListAsync(cancellationToken);

        var existingRegularDefinitionIdsSet = existingRegularDefinitionIdsInTarget.ToHashSet();
        var maxOrder = await dbContext.Expenses
            .Where(x => x.MonthPlanId == targetMonthPlan.Id)
            .Select(x => (int?)x.Order)
            .MaxAsync(cancellationToken) ?? 0;

        var createdCount = 0;
        foreach (var sourceExpense in sourceExpenses)
        {
            if (sourceExpense.RegularExpenseDefinitionId.HasValue
                && existingRegularDefinitionIdsSet.Contains(sourceExpense.RegularExpenseDefinitionId.Value))
            {
                continue;
            }

            if (sourceExpense.LoanInstallmentId.HasValue)
            {
                continue;
            }

            maxOrder++;
            dbContext.Expenses.Add(new Expense
            {
                MonthPlanId = targetMonthPlan.Id,
                Order = maxOrder,
                Name = sourceExpense.Name,
                CategoryId = sourceExpense.CategoryId,
                TagId = sourceExpense.TagId,
                RegularExpenseDefinitionId = sourceExpense.RegularExpenseDefinitionId,
                PlannedAmount = sourceExpense.PlannedAmount,
                ActualAmount = 0,
                ShowRemainingInUI = sourceExpense.ShowRemainingInUI
            });

            createdCount++;
            if (sourceExpense.RegularExpenseDefinitionId.HasValue)
            {
                existingRegularDefinitionIdsSet.Add(sourceExpense.RegularExpenseDefinitionId.Value);
            }
        }

        if (createdCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return createdCount;
    }

    public async Task<int> CopySelectedExpensesToNextMonthAsync(
        CopySelectedExpensesToNextMonthRequest request,
        CancellationToken cancellationToken)
    {
        CopySelectedExpensesToNextMonthValidator.ValidateOrThrowBadRequest(request);

        var nextMonthDate = new DateTime(request.Year, request.Month, 1).AddMonths(1);
        return await CopySelectedExpensesToMonthAsync(new CopySelectedExpensesToMonthRequest
        {
            Year = request.Year,
            Month = request.Month,
            TargetYear = nextMonthDate.Year,
            TargetMonth = nextMonthDate.Month,
            ExpenseIds = request.ExpenseIds
        }, cancellationToken);
    }

    public async Task<ExpenseLineItemDto> CreateExpenseLineItemAsync(CreateExpenseLineItemRequest request,
        CancellationToken cancellationToken)
    {
        CreateExpenseLineItemValidator.ValidateOrThrowBadRequest(request);

        var normalizedDescription = request.Description;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expense = await dbContext.Expenses
                          .Include(x => x.Category)
                          .Include(x => x.Tag)
                          .Include(x => x.MonthPlan)
                          .Include(x => x.LineItems)
                          .FirstOrDefaultAsync(x => x.Id == request.ExpenseId, cancellationToken)
                      ?? throw new NotFoundException("Expense not found.");

        BudgetHelper.EnsureMonthIsOpen(expense.MonthPlan);

        var envelopeUsageBefore = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            expense.MonthPlanId,
            expense.CategoryId,
            cancellationToken);

        var supportsLineItems = expense.Tag?.SupportsLineItemsOverride ?? expense.Category.SupportsLineItems;
        if (!supportsLineItems)
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

            if (expense.TagId.HasValue && tag.ParentTagId != expense.TagId.Value)
            {
                throw new BadRequestException("Selected line item tag must be a sub-tag of expense main tag.");
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

        var envelopeUsageAfter = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            expense.MonthPlanId,
            expense.CategoryId,
            cancellationToken);

        await EmitBudgetExceededEventIfNeededAsync(
            envelopeUsageBefore,
            envelopeUsageAfter,
            expense.MonthPlan.Year,
            expense.MonthPlan.Month,
            cancellationToken);

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
                           .Include(x => x.Expense)
                           .ThenInclude(x => x.Tag)
                           .Include(x => x.Expense)
                           .ThenInclude(x => x.MonthPlan)
                           .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException("Line item not found.");

        BudgetHelper.EnsureMonthIsOpen(lineItem.Expense.MonthPlan);

        var envelopeUsageBefore = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            lineItem.Expense.MonthPlanId,
            lineItem.Expense.CategoryId,
            cancellationToken);

        var supportsLineItems = lineItem.Expense.Tag?.SupportsLineItemsOverride ?? lineItem.Expense.Category.SupportsLineItems;
        if (!supportsLineItems)
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

            if (lineItem.Expense.TagId.HasValue
                && tag.ParentTagId != lineItem.Expense.TagId.Value)
            {
                throw new BadRequestException("Selected line item tag must be a sub-tag of expense main tag.");
            }
        }

        lineItem.Description = normalizedDescription;
        lineItem.Amount = request.Amount;
        lineItem.OccurredAt = request.OccurredAt;
        lineItem.TagId = request.TagId;

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateActualAmountAsync(dbContext, lineItem.ExpenseId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var envelopeUsageAfter = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            lineItem.Expense.MonthPlanId,
            lineItem.Expense.CategoryId,
            cancellationToken);

        await EmitBudgetExceededEventIfNeededAsync(
            envelopeUsageBefore,
            envelopeUsageAfter,
            lineItem.Expense.MonthPlan.Year,
            lineItem.Expense.MonthPlan.Month,
            cancellationToken);

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
                           .Include(x => x.Expense)
                           .ThenInclude(x => x.MonthPlan)
                           .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException("Line item not found.");

        BudgetHelper.EnsureMonthIsOpen(lineItem.Expense.MonthPlan);

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
                          .Include(x => x.MonthPlan)
                          .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                      ?? throw new NotFoundException("Expense not found.");

        BudgetHelper.EnsureMonthIsOpen(expense.MonthPlan);

        var envelopeUsageBefore = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            expense.MonthPlanId,
            request.CategoryId,
            cancellationToken);

        var category = await dbContext.Categories
                           .AsNoTracking()
                           .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken)
                       ?? throw new NotFoundException("Category not found.");

        var normalizedTagId = await EnsureCategoryAndTagValidAsync(
            dbContext,
            request.CategoryId,
            request.TagId,
            cancellationToken);
        var normalizedTagName = normalizedTagId.HasValue
            ? await dbContext.Tags
                .AsNoTracking()
                .Where(x => x.Id == normalizedTagId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        expense.Name = normalizedName;
        expense.CategoryId = request.CategoryId;
        expense.TagId = normalizedTagId;
        expense.PlannedAmount = request.PlannedAmount;
        if (!await dbContext.ExpenseLineItems.AnyAsync(x => x.ExpenseId == expense.Id, cancellationToken))
        {
            expense.ActualAmount = request.ActualAmount;
        }

        expense.ShowRemainingInUI = request.ShowRemainingInUI;

        await dbContext.SaveChangesAsync(cancellationToken);

        await RecalculateActualAmountAsync(dbContext, expense.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var envelopeUsageAfter = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            expense.MonthPlanId,
            request.CategoryId,
            cancellationToken);

        await EmitBudgetExceededEventIfNeededAsync(
            envelopeUsageBefore,
            envelopeUsageAfter,
            expense.MonthPlan.Year,
            expense.MonthPlan.Month,
            cancellationToken);

        return await BuildExpenseDtoAsync(dbContext, expense.Id, cancellationToken, category.Name, normalizedTagName);
    }

    public async Task DeleteExpenseAsync(DeleteExpenseRequest request, CancellationToken cancellationToken)
    {
        DeleteExpenseValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expense = await dbContext.Expenses
                          .Include(x => x.MonthPlan)
                          .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                      ?? throw new NotFoundException("Expense not found.");

        BudgetHelper.EnsureMonthIsOpen(expense.MonthPlan);

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
        var monthPlanState = await GetOrCreateMonthPlanStateAsync(dbContext, year, month, cancellationToken);
        return monthPlanState.MonthPlan;
    }

    private static async Task<MonthPlanState> GetOrCreateMonthPlanStateAsync(
        ApplicationDbContext dbContext,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var monthPlan = await dbContext.MonthPlans
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month, cancellationToken);

        if (monthPlan is not null)
        {
            return new MonthPlanState(monthPlan, false);
        }

        monthPlan = new MonthPlan
        {
            Year = year,
            Month = month,
            IsClosed = false
        };

        dbContext.MonthPlans.Add(monthPlan);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MonthPlanState(monthPlan, true);
    }

    private static async Task<IReadOnlyList<Expense>> LoadMonthExpensesAsync(
        ApplicationDbContext dbContext,
        int year,
        int month,
        IReadOnlyCollection<int>? expenseIds,
        CancellationToken cancellationToken)
    {
        IQueryable<Expense> query = dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.MonthPlan.Year == year && x.MonthPlan.Month == month)
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .Include(x => x.RegularExpenseDefinition)
            .Include(x => x.LoanInstallment!)
            .ThenInclude(x => x.Loan)
            .Include(x => x.LineItems);

        if (expenseIds is not null)
        {
            var selectedIds = expenseIds.ToArray();
            query = query.Where(x => selectedIds.Contains(x.Id));
        }

        query = query
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id);

        return await query.ToListAsync(cancellationToken);
    }

    private static MonthPlanExpenseSuggestionDto BuildMonthPlanExpenseSuggestionDto(
        Expense expense,
        int sourceYear,
        int sourceMonth,
        IReadOnlySet<RecurringExpenseKey> activeRecurringExpenseKeys)
    {
        var sourceActualAmount = GetExpenseActualAmountForSuggestion(expense);
        return new MonthPlanExpenseSuggestionDto
        {
            SourceExpenseId = expense.Id,
            Name = expense.Name,
            CategoryId = expense.CategoryId,
            CategoryName = expense.Category.Name,
            TagId = expense.TagId,
            TagName = expense.Tag?.Name,
            SourcePlannedAmount = expense.PlannedAmount,
            SourceActualAmount = sourceActualAmount,
            SuggestedPlannedAmount = CalculateSuggestedPlannedAmount(
                sourceActualAmount > 0 ? sourceActualAmount : expense.PlannedAmount),
            Reason = $"Ten sam miesiąc w poprzednim roku ({sourceYear}-{sourceMonth:00})",
            IsAvailable = IsHistoricalSuggestionAvailable(expense, activeRecurringExpenseKeys),
            UnavailableReason = GetHistoricalSuggestionUnavailableReason(expense, activeRecurringExpenseKeys)
        };
    }

    private static decimal GetExpenseActualAmountForSuggestion(Expense expense)
    {
        return ExpenseActualAmountCalculator.GetEffectiveActualAmount(expense);
    }

    private static bool IsHistoricalSuggestionAvailable(
        Expense expense,
        IReadOnlySet<RecurringExpenseKey> activeRecurringExpenseKeys)
    {
        if (expense.RegularExpenseDefinitionId.HasValue && expense.RegularExpenseDefinition?.IsActive == true)
        {
            return false;
        }

        if (activeRecurringExpenseKeys.Contains(RecurringExpenseKey.FromExpense(expense)))
        {
            return false;
        }

        if (expense.LoanInstallmentId.HasValue && expense.LoanInstallment?.Loan?.IsActive == true)
        {
            return false;
        }

        return true;
    }

    private static string? GetHistoricalSuggestionUnavailableReason(
        Expense expense,
        IReadOnlySet<RecurringExpenseKey> activeRecurringExpenseKeys)
    {
        if (expense.RegularExpenseDefinitionId.HasValue && expense.RegularExpenseDefinition?.IsActive == true)
        {
            return "Wydatek cykliczny zostanie automatycznie zsynchronizowany przy utworzeniu miesiąca.";
        }

        if (expense.LoanInstallmentId.HasValue && expense.LoanInstallment?.Loan?.IsActive == true)
        {
            return "Rata kredytu zostanie automatycznie zsynchronizowana przy utworzeniu miesiąca.";
        }

        if (activeRecurringExpenseKeys.Contains(RecurringExpenseKey.FromExpense(expense)))
        {
            return "Podobny aktywny wydatek cykliczny zostanie automatycznie dodany przy utworzeniu miesiąca.";
        }

        return null;
    }

    private static async Task<HashSet<RecurringExpenseKey>> LoadActiveRecurringExpenseKeysAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var definitions = await dbContext.RegularExpenseDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new
            {
                x.Name,
                x.CategoryId,
                x.TagId
            })
            .ToListAsync(cancellationToken);

        return definitions
            .Select(x => new RecurringExpenseKey(x.Name, x.CategoryId, x.TagId))
            .ToHashSet();
    }

    private static decimal CalculateSuggestedPlannedAmount(decimal basisAmount)
    {
        if (basisAmount <= 0)
        {
            return 0;
        }

        var bufferedAmount = basisAmount * 1.10m;
        var roundingScale = bufferedAmount < 500m ? 10m : 100m;
        var roundedUpAmount = Math.Ceiling(bufferedAmount / roundingScale) * roundingScale;
        return decimal.Round(roundedUpAmount, 2, MidpointRounding.AwayFromZero);
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

    private static decimal CalculateRemainingContribution(ExpenseDto expense)
    {
        if (expense.PlannedAmount <= 0)
        {
            return 0;
        }

        if (expense.ShowRemainingInUI)
        {
            return Math.Max(0, expense.RemainingAmount);
        }

        return expense.ActualAmount == 0 ? expense.PlannedAmount : 0;
    }

    private static decimal CalculateOutsidePlanContribution(decimal plannedAmount, decimal actualAmount)
    {
        if (plannedAmount <= 0)
        {
            return actualAmount;
        }

        return Math.Max(actualAmount - plannedAmount, 0m);
    }

    private static decimal SumAccountsOverviewClosingBalances(
        IReadOnlyList<AccountBalanceSnapshot> balances,
        int year,
        int month,
        bool isClosedMonth)
    {
        return balances
            .Where(x => isClosedMonth || IsAccountApplicableForMonth(
                    x.ActiveFromUtc,
                    x.IsArchived,
                    x.ArchivedAtUtc,
                    x.UpdatedAtUtc,
                    year,
                    month))
            .GroupBy(x => x.AccountId)
            .Sum(group => group
                .Where(x => x.Year == year && x.Month == month)
                .Select(x => x.ClosingBalance)
                .FirstOrDefault());
    }

    private static int ToMonthKey(int year, int month)
    {
        return (year * 12) + month;
    }

    private static decimal? GetClosingBalanceForApplicableAccountInMonth(
        IReadOnlyList<AccountBalanceSnapshot> balances,
        int accountId,
        DateTime? activeFromUtc,
        bool isArchived,
        DateTime? archivedAtUtc,
        DateTime updatedAtUtc,
        int year,
        int month)
    {
        var closingBalance = balances
            .Where(x => x.AccountId == accountId && x.Year == year && x.Month == month)
            .Select(x => x.ClosingBalance)
            .Cast<decimal?>()
            .FirstOrDefault();

        if (closingBalance.HasValue)
        {
            return closingBalance;
        }

        return IsAccountApplicableForMonth(activeFromUtc, isArchived, archivedAtUtc, updatedAtUtc, year, month)
            ? 0m
            : null;
    }

    private static bool IsAccountApplicableForMonth(
        DateTime? activeFromUtc,
        bool isArchived,
        DateTime? archivedAtUtc,
        DateTime updatedAtUtc,
        int year,
        int month)
    {
        var nextMonthStartUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        if (activeFromUtc is not null && activeFromUtc >= nextMonthStartUtc)
        {
            return false;
        }

        return !isArchived || (archivedAtUtc ?? updatedAtUtc) >= nextMonthStartUtc;
    }

    private static TagHierarchySnapshot ResolveRootAndSubTag(
        int? tagId,
        IReadOnlyDictionary<int, TagSnapshot> tagById)
    {
        if (!tagId.HasValue || !tagById.TryGetValue(tagId.Value, out var tag))
        {
            return new TagHierarchySnapshot(null, null, null, null);
        }

        if (!tag.ParentTagId.HasValue)
        {
            return new TagHierarchySnapshot(tag.Id, tag.Name, null, null);
        }

        if (tagById.TryGetValue(tag.ParentTagId.Value, out var parentTag))
        {
            return new TagHierarchySnapshot(parentTag.Id, parentTag.Name, tag.Id, tag.Name);
        }

        return new TagHierarchySnapshot(tag.ParentTagId.Value, null, tag.Id, tag.Name);
    }

    private static List<int> GetDescendants(IReadOnlyList<TagSnapshot> tags, int tagId)
    {
        var descendants = new List<int>();
        var queue = new Queue<int>();
        queue.Enqueue(tagId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = tags
                .Where(x => x.ParentTagId == current)
                .Select(x => x.Id)
                .ToList();

            foreach (var child in children)
            {
                descendants.Add(child);
                queue.Enqueue(child);
            }
        }

        return descendants;
    }

    private static int CalculateTagDepth(IReadOnlyList<TagSnapshot> tags, int tagId)
    {
        var depth = 0;
        var current = tags.FirstOrDefault(x => x.Id == tagId);

        while (current?.ParentTagId is not null)
        {
            depth++;
            current = tags.FirstOrDefault(x => x.Id == current.ParentTagId.Value);
        }

        return depth;
    }

    private sealed record AccountBalanceSnapshot(
        int AccountId,
        int AccountType,
        DateTime? ActiveFromUtc,
        bool IsArchived,
        DateTime? ArchivedAtUtc,
        DateTime UpdatedAtUtc,
        int Year,
        int Month,
        decimal ClosingBalance);
    private sealed record ExpenseYearSnapshot(
        int ExpenseId,
        int CategoryId,
        string CategoryName,
        int Month,
        int? TagId,
        decimal PlannedAmount,
        decimal ActualAmount);

    private sealed record ExpenseLineItemYearSnapshot(
        int ExpenseId,
        int CategoryId,
        int Month,
        int? ExpenseTagId,
        int? TagId,
        decimal Amount);

    private sealed record ExpenseHistorySearchSnapshot(
        int ExpenseId,
        string Name,
        int Year,
        int Month,
        int CategoryId,
        string CategoryName,
        int? TagId,
        decimal PlannedAmount,
        decimal ActualAmount,
        IReadOnlyList<ExpenseLineItemSearchSnapshot> LineItems);

    private sealed record ExpenseLineItemSearchSnapshot(string Description, int? TagId, decimal Amount);

    private sealed record TagSnapshot(int Id, int CategoryId, int? ParentTagId, string Name);
    private sealed record TagHierarchySnapshot(int? RootTagId, string? RootTagName, int? SubTagId, string? SubTagName);
    private sealed record MonthPlanState(MonthPlan MonthPlan, bool WasCreated);

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

        expense.ActualAmount = ExpenseActualAmountCalculator.GetEffectiveActualAmount(expense);
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

    private static async Task<RegularExpenseDefinitionDto> BuildRegularExpenseDefinitionDtoAsync(
        ApplicationDbContext dbContext,
        int definitionId,
        CancellationToken cancellationToken)
    {
        var definition = await dbContext.RegularExpenseDefinitions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .FirstOrDefaultAsync(x => x.Id == definitionId, cancellationToken)
            ?? throw new NotFoundException("Regular expense definition not found.");

        return definition.MapRegularExpenseDefinitionToDto();
    }

    private static async Task<int?> EnsureCategoryAndTagValidAsync(
        ApplicationDbContext dbContext,
        int categoryId,
        int? tagId,
        CancellationToken cancellationToken)
    {
        var categoryExists = await dbContext.Categories
            .AsNoTracking()
            .AnyAsync(x => x.Id == categoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new NotFoundException("Category not found.");
        }

        if (!tagId.HasValue)
        {
            return null;
        }

        var tag = await dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tagId.Value, cancellationToken)
            ?? throw new NotFoundException("Tag not found.");

        if (tag.CategoryId != categoryId)
        {
            throw new BadRequestException("Selected tag does not belong to selected category.");
        }

        return tag.Id;
    }

    private static async Task SyncRegularExpensesForMonthAsync(
        ApplicationDbContext dbContext,
        MonthPlan monthPlan,
        CancellationToken cancellationToken)
    {
        var definitions = await dbContext.RegularExpenseDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (definitions.Count == 0)
        {
            return;
        }

        var existingDefinitionIds = await dbContext.Expenses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.UserId == dbContext.CurrentBudgetOwnerUserId
                        && x.MonthPlanId == monthPlan.Id
                        && x.RegularExpenseDefinitionId.HasValue)
            .Select(x => x.RegularExpenseDefinitionId!.Value)
            .ToListAsync(cancellationToken);

        var existingSet = existingDefinitionIds.ToHashSet();

        var maxOrder = await dbContext.Expenses
            .Where(x => x.MonthPlanId == monthPlan.Id)
            .Select(x => (int?)x.Order)
            .MaxAsync(cancellationToken) ?? 0;

        foreach (var definition in definitions)
        {
            if (existingSet.Contains(definition.Id))
            {
                continue;
            }

            maxOrder++;
            dbContext.Expenses.Add(new Expense
            {
                MonthPlanId = monthPlan.Id,
                Order = maxOrder,
                Name = definition.Name,
                CategoryId = definition.CategoryId,
                TagId = definition.TagId,
                RegularExpenseDefinitionId = definition.Id,
                PlannedAmount = definition.Amount,
                ActualAmount = 0,
                ShowRemainingInUI = definition.ShowRemainingInUI
            });
        }
    }

    private async Task EmitBudgetExceededEventIfNeededAsync(
        EnvelopeUsageSnapshot? before,
        EnvelopeUsageSnapshot? after,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (after is null)
        {
            return;
        }

        var wasExceeded = before is not null && before.SpentAmount > before.Limit;
        var isExceeded = after.SpentAmount > after.Limit;

        if (wasExceeded || !isExceeded)
        {
            return;
        }

        await appEventPublisher.PublishAsync(new BudgetExceededEvent
        {
            CategoryId = after.CategoryId,
            CategoryName = after.CategoryName,
            Year = year,
            Month = month,
            EnvelopeLimit = after.Limit,
            SpentAmount = after.SpentAmount
        }, cancellationToken);
    }

    private static async Task<EnvelopeUsageSnapshot?> GetEnvelopeUsageSnapshotAsync(
        ApplicationDbContext dbContext,
        int monthPlanId,
        int categoryId,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (category?.EnvelopeLimit is not > 0)
        {
            return null;
        }

        var spentAmount = await dbContext.Expenses
            .Where(x => x.MonthPlanId == monthPlanId && x.CategoryId == categoryId)
            .SumAsync(x => x.ActualAmount, cancellationToken);

        return new EnvelopeUsageSnapshot(category.Id, category.Name, category.EnvelopeLimit.Value, spentAmount);
    }

    private sealed record RecurringExpenseKey(string Name, int CategoryId, int? TagId)
    {
        public static RecurringExpenseKey FromExpense(Expense expense)
        {
            return new RecurringExpenseKey(expense.Name, expense.CategoryId, expense.TagId);
        }

        public bool Equals(RecurringExpenseKey? other)
        {
            return other is not null
                   && CategoryId == other.CategoryId
                   && TagId == other.TagId
                   && string.Equals(Name.Trim(), other.Name.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Name.Trim()), CategoryId, TagId);
        }
    }

    private sealed record EnvelopeUsageSnapshot(int CategoryId, string CategoryName, decimal Limit, decimal SpentAmount);
}
