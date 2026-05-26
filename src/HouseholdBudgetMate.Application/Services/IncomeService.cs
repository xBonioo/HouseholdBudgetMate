using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Helpers;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Application.Mapping;
using HouseholdBudgetMate.Application.Validation;
using HouseholdBudgetMate.Application.Validation.Common;
using HouseholdBudgetMate.Application.Validation.Incomes;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class IncomeService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider) : IIncomeService
{
    private static readonly YearMonthRequestValidator YearMonthValidator = new();
    private static readonly DateInMonthRequestValidator DateInMonthValidator = new();
    
    private static readonly CreateRegularIncomeDefinitionRequestValidator CreateRegularDefinitionValidator = new();
    private static readonly CreateIncomeRequestValidator CreateIncomeValidator = new();
    private static readonly UpdateRegularIncomeDefinitionRequestValidator UpdateRegularDefinitionValidator = new();
    private static readonly UpdateIncomeRequestValidator UpdateIncomeValidator = new();
    private static readonly DeleteRegularIncomeDefinitionRequestValidator DeleteRegularDefinitionValidator = new();
    private static readonly DeleteIncomeRequestValidator DeleteIncomeValidator = new();

    public async Task<IReadOnlyList<RegularIncomeDefinitionDto>> GetRegularDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definitions = await dbContext.RegularIncomeDefinitions
            .AsNoTracking()
            .Include(x => x.Account)
            .OrderBy(x => x.Name)
            .Select(x => x.MapDefinitionToDto())
            .ToListAsync(cancellationToken);

        return definitions;
    }

    public async Task<RegularIncomeDefinitionDto> CreateRegularDefinitionAsync(
        CreateRegularIncomeDefinitionRequest request, CancellationToken cancellationToken)
    {
        CreateRegularDefinitionValidator.ValidateOrThrowBadRequest(request);
        var normalizedName = request.Name;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var account = await dbContext.Accounts
                          .AsNoTracking()
                          .FirstOrDefaultAsync(x => x.Id == request.AccountId, cancellationToken)
                      ?? throw new NotFoundException("Account not found.");

        var definition = new RegularIncomeDefinition
        {
            Name = normalizedName,
            Amount = request.Amount,
            DayOfMonth = request.DayOfMonth,
            AccountId = request.AccountId,
            IsActive = true
        };

        dbContext.RegularIncomeDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RegularIncomeDefinitionDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Amount = definition.Amount,
            DayOfMonth = definition.DayOfMonth,
            AccountId = definition.AccountId,
            AccountName = account.Name,
            IsActive = definition.IsActive
        };
    }

    public async Task<RegularIncomeDefinitionDto> UpdateRegularDefinitionAsync(
        UpdateRegularIncomeDefinitionRequest request, CancellationToken cancellationToken)
    {
        UpdateRegularDefinitionValidator.ValidateOrThrowBadRequest(request);
        var normalizedName = request.Name;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definition = await dbContext.RegularIncomeDefinitions
                             .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException("Regular income definition not found.");

        var account = await dbContext.Accounts
                          .AsNoTracking()
                          .FirstOrDefaultAsync(x => x.Id == request.AccountId, cancellationToken)
                      ?? throw new NotFoundException("Account not found.");

        definition.Name = normalizedName;
        definition.Amount = request.Amount;
        definition.DayOfMonth = request.DayOfMonth;
        definition.AccountId = request.AccountId;
        definition.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RegularIncomeDefinitionDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Amount = definition.Amount,
            DayOfMonth = definition.DayOfMonth,
            AccountId = definition.AccountId,
            AccountName = account.Name,
            IsActive = definition.IsActive
        };
    }

    public async Task DeleteRegularDefinitionAsync(DeleteRegularIncomeDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        DeleteRegularDefinitionValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definition = await dbContext.RegularIncomeDefinitions
                             .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException("Regular income definition not found.");

        if (!definition.IsActive)
        {
            return;
        }

        definition.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRegularDefinitionPermanentlyAsync(
        DeleteRegularIncomeDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        DeleteRegularDefinitionValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definition = await dbContext.RegularIncomeDefinitions
                             .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException("Regular income definition not found.");

        dbContext.RegularIncomeDefinitions.Remove(definition);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncRegularIncomesForMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlan = await dbContext.MonthPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month, cancellationToken);
        if (monthPlan?.IsClosed == true)
        {
            return;
        }

        var definitions = await dbContext.RegularIncomeDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        if (definitions.Count == 0)
        {
            return;
        }

        var existingDefinitionIds = await dbContext.Incomes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.UserId == dbContext.CurrentBudgetOwnerUserId
                        && x.Year == year
                        && x.Month == month
                        && x.IsRegular
                        && x.RegularIncomeDefinitionId.HasValue)
            .Select(x => x.RegularIncomeDefinitionId!.Value)
            .ToListAsync(cancellationToken);

        var existingSet = existingDefinitionIds.ToHashSet();
        foreach (var definition in definitions)
        {
            if (existingSet.Contains(definition.Id))
            {
                continue;
            }

            var day = Math.Min(definition.DayOfMonth, DateTime.DaysInMonth(year, month));
            dbContext.Incomes.Add(new Income
            {
                Year = year,
                Month = month,
                Name = definition.Name,
                Amount = definition.Amount,
                ExpectedDayOfMonth = new DateOnly(year, month, day),
                AccountId = definition.AccountId,
                IsRegular = true,
                RegularIncomeDefinitionId = definition.Id
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> AddRegularDefinitionToMonthAsync(int definitionId, int year, int month,
        CancellationToken cancellationToken)
    {
        if (definitionId <= 0)
        {
            throw new BadRequestException("Definition ID must be greater than 0.");
        }

        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureMonthIsOpenAsync(dbContext, year, month, cancellationToken);

        var definition = await dbContext.RegularIncomeDefinitions
                             .AsNoTracking()
                             .FirstOrDefaultAsync(x => x.Id == definitionId, cancellationToken)
                         ?? throw new NotFoundException("Regular income definition not found.");

        if (!definition.IsActive)
        {
            return false;
        }

        var existsInMonth = await dbContext.Incomes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == dbContext.CurrentBudgetOwnerUserId
                     && x.Year == year
                     && x.Month == month
                     && x.IsRegular
                     && x.RegularIncomeDefinitionId == definitionId,
                cancellationToken);

        if (existsInMonth)
        {
            return false;
        }

        var day = Math.Min(definition.DayOfMonth, DateTime.DaysInMonth(year, month));
        dbContext.Incomes.Add(new Income
        {
            Year = year,
            Month = month,
            Name = definition.Name,
            Amount = definition.Amount,
            ExpectedDayOfMonth = new DateOnly(year, month, day),
            AccountId = definition.AccountId,
            IsRegular = true,
            RegularIncomeDefinitionId = definition.Id,
            IsDeleted = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<IncomeDto>> GetMonthIncomesAsync(int year, int month,
        CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var incomes = await dbContext.Incomes
            .AsNoTracking()
            .Where(x => x.Year == year && x.Month == month)
            .Include(x => x.Account)
            .OrderBy(x => x.ExpectedDayOfMonth)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return incomes.Select(x => x.MapToDto()).ToList();
    }

    public async Task<IncomeDto> CreateIncomeAsync(CreateIncomeRequest request, CancellationToken cancellationToken)
    {
        CreateIncomeValidator.ValidateOrThrowBadRequest(request);
        var normalizedName = request.Name;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureMonthIsOpenAsync(dbContext, request.Year, request.Month, cancellationToken);

        var account = await dbContext.Accounts
                          .AsNoTracking()
                          .FirstOrDefaultAsync(x => x.Id == request.AccountId, cancellationToken)
                      ?? throw new NotFoundException("Account not found.");

        var income = new Income
        {
            Year = request.Year,
            Month = request.Month,
            Name = normalizedName,
            Amount = request.Amount,
            ExpectedDayOfMonth = request.ExpectedDayOfMonth,
            AccountId = request.AccountId,
            IsRegular = request.IsRegular,
            RegularIncomeDefinitionId = null,
            IsDeleted = false
        };

        dbContext.Incomes.Add(income);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new IncomeDto
        {
            Id = income.Id,
            Year = income.Year,
            Month = income.Month,
            Name = income.Name,
            Amount = income.Amount,
            ExpectedDayOfMonth = income.ExpectedDayOfMonth,
            AccountId = income.AccountId,
            AccountName = account.Name,
            IsRegular = income.IsRegular
        };
    }

    public async Task<IncomeDto> UpdateIncomeAsync(UpdateIncomeRequest request, CancellationToken cancellationToken)
    {
        UpdateIncomeValidator.ValidateOrThrowBadRequest(request);
        var normalizedName = request.Name;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var income = await dbContext.Incomes
                         .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                     ?? throw new NotFoundException("Income not found.");

        await EnsureMonthIsOpenAsync(dbContext, income.Year, income.Month, cancellationToken);

        DateInMonthValidator.ValidateOrThrowBadRequest(new DateInMonthRequest(
            request.ExpectedDayOfMonth,
            income.Year,
            income.Month,
            "Expected day must belong to selected month and year."));

        var account = await dbContext.Accounts
                          .AsNoTracking()
                          .FirstOrDefaultAsync(x => x.Id == request.AccountId, cancellationToken)
                      ?? throw new NotFoundException("Account not found.");

        income.Name = normalizedName;
        income.Amount = request.Amount;
        income.ExpectedDayOfMonth = request.ExpectedDayOfMonth;
        income.AccountId = request.AccountId;
        income.IsRegular = request.IsRegular;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new IncomeDto
        {
            Id = income.Id,
            Year = income.Year,
            Month = income.Month,
            Name = income.Name,
            Amount = income.Amount,
            ExpectedDayOfMonth = income.ExpectedDayOfMonth,
            AccountId = income.AccountId,
            AccountName = account.Name,
            IsRegular = income.IsRegular
        };
    }

    public async Task DeleteIncomeAsync(DeleteIncomeRequest request, CancellationToken cancellationToken)
    {
        DeleteIncomeValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var income = await dbContext.Incomes
                         .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                     ?? throw new NotFoundException("Income not found.");

        await EnsureMonthIsOpenAsync(dbContext, income.Year, income.Month, cancellationToken);

        income.IsDeleted = true;
        income.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LiveBalanceDto> GetLiveBalanceAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));
        var today = dateTimeProvider.GetLocalDateOnly();
        var previousMonthDate = new DateTime(year, month, 1).AddMonths(-1);
        var previousYear = previousMonthDate.Year;
        var previousMonth = previousMonthDate.Month;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.Type != (int)AccountType.Savings)
            .Include(x => x.MonthBalances)
            .ToListAsync(cancellationToken);

        decimal accountBaseTotal = 0;
        var missingBalanceAccountNames = new List<string>();
        foreach (var account in accounts)
        {
            var precedingMonthBalance = account.MonthBalances
                .FirstOrDefault(x => x.Year == previousYear && x.Month == previousMonth);

            if (precedingMonthBalance is null)
            {
                missingBalanceAccountNames.Add(account.Name);
                continue;
            }

            accountBaseTotal += precedingMonthBalance.ClosingBalance;
        }

        var incomesTotal = await dbContext.Incomes
            .AsNoTracking()
            .Where(x => x.Year == year && x.Month == month)
            .Where(x => x.ExpectedDayOfMonth <= today)
            .SumAsync(x => x.Amount, cancellationToken);

        decimal expensesTotal = 0;
        decimal savingsTransfersTotal = 0;
        decimal outstandingPlannedExpensesReserveTotal = 0;
        decimal pendingSavingsTransfersReserveTotal = 0;
        var monthPlan = await dbContext.MonthPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month, cancellationToken);

        if (monthPlan is not null)
        {
            expensesTotal = await dbContext.Expenses
                .AsNoTracking()
                .Where(x => x.MonthPlanId == monthPlan.Id)
                .SumAsync(x => x.ActualAmount, cancellationToken);

            outstandingPlannedExpensesReserveTotal = await dbContext.Expenses
                .AsNoTracking()
                .Where(x => x.MonthPlanId == monthPlan.Id
                            && x.PlannedAmount > 0
                            && x.ActualAmount < x.PlannedAmount)
                .SumAsync(x => x.PlannedAmount - x.ActualAmount, cancellationToken);

            savingsTransfersTotal = await dbContext.MonthSavingsTransferItems
                .AsNoTracking()
                .Where(x => x.MonthPlanId == monthPlan.Id)
                .Where(x => x.TransferDate <= today)
                .SumAsync(x => x.Amount, cancellationToken);

            pendingSavingsTransfersReserveTotal = await dbContext.MonthSavingsTransferItems
                .AsNoTracking()
                .Where(x => x.MonthPlanId == monthPlan.Id)
                .Where(x => x.TransferDate > today)
                .SumAsync(x => x.Amount, cancellationToken);
        }

        var currentBalance = accountBaseTotal + incomesTotal - expensesTotal - savingsTransfersTotal;

        return new LiveBalanceDto
        {
            AccountsBaseTotal = accountBaseTotal,
            IncomesTotal = incomesTotal,
            ExpensesTotal = expensesTotal,
            SavingsTransfersTotal = savingsTransfersTotal,
            CurrentBalance = currentBalance,
            OutstandingPlannedExpensesReserveTotal = outstandingPlannedExpensesReserveTotal,
            PendingSavingsTransfersReserveTotal = pendingSavingsTransfersReserveTotal,
            SafeToSpendAmount = currentBalance
                                - outstandingPlannedExpensesReserveTotal
                                - pendingSavingsTransfersReserveTotal,
            HasCompleteBalanceBase = missingBalanceAccountNames.Count == 0,
            MissingBalanceAccountNames = missingBalanceAccountNames
        };
    }

    private static async Task EnsureMonthIsOpenAsync(
        ApplicationDbContext dbContext,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var monthPlan = await dbContext.MonthPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month, cancellationToken);

        BudgetHelper.EnsureMonthIsOpen(monthPlan);
    }
}
