using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Security;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class MonthlyBudgetingLoopTests
{
    private const int Year = 2026;
    private const int Month = 4;
    private const string VisibleUserId = "visible-admin";
    private const string VisibleUserPin = "2468";
    private static readonly DateTime TodayUtc = new(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MonthlyBudgetingLoop_Should_Update_LiveBalance_Kpi_Savings_And_Lifecycle()
    {
        var currentUser = new CurrentUserContext();
        currentUser.SetInteractiveUser(VisibleUserId, User.DefaultUserId);
        var options = NewOptions();
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);
        var services = CreateServices(factory);

        var categoryId = await SeedVisibleProfileAndFinancialBaseAsync(options, currentUser);

        Assert.Equal(VisibleUserId, currentUser.UserId);
        Assert.Equal(User.DefaultUserId, currentUser.BudgetOwnerUserId);

        var initial = await services.IncomeService.GetLiveBalanceAsync(Year, Month, CancellationToken.None);
        Assert.True(initial.HasCompleteBalanceBase);
        Assert.Equal(3000m, initial.AccountsBaseTotal);
        Assert.Equal(5000m, initial.IncomesTotal);
        Assert.Equal(8000m, initial.CurrentBalance);

        var plannedExpense = await services.ExpenseService.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = Year,
            Month = Month,
            Name = "Czynsz",
            CategoryId = categoryId,
            PlannedAmount = 1200m,
            ActualAmount = 0m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var afterPlannedExpense = await ReadLoopStateAsync(services);
        AssertLoopState(afterPlannedExpense, liveBalance: 8000m, planRemaining: 1200m, savingsTransfersTotal: 0m);
        Assert.Equal(1200m, afterPlannedExpense.Month.Kpi.PlannedTotal);
        Assert.Equal(0m, afterPlannedExpense.Month.Kpi.SpentTotal);

        await services.ExpenseService.UpdateExpenseAsync(new UpdateExpenseRequest
        {
            Id = plannedExpense.Id,
            Name = "Czynsz",
            CategoryId = categoryId,
            PlannedAmount = 1200m,
            ActualAmount = 450m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var afterRealSpend = await ReadLoopStateAsync(services);
        AssertLoopState(afterRealSpend, liveBalance: 7550m, planRemaining: 750m, savingsTransfersTotal: 0m);
        Assert.Equal(450m, afterRealSpend.Month.Kpi.SpentTotal);

        await services.ExpenseService.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = Year,
            Month = Month,
            Name = "Awaria",
            CategoryId = categoryId,
            PlannedAmount = 0m,
            ActualAmount = 125m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var afterUnexpectedExpense = await ReadLoopStateAsync(services);
        AssertLoopState(afterUnexpectedExpense, liveBalance: 7425m, planRemaining: 750m, savingsTransfersTotal: 0m);
        Assert.Equal(575m, afterUnexpectedExpense.Month.Kpi.SpentTotal);

        await services.ExpenseService.CreateMonthSavingsTransferItemAsync(new CreateMonthSavingsTransferItemRequest
        {
            Year = Year,
            Month = Month,
            Amount = 600m,
            TransferDate = new DateOnly(2026, 4, 20)
        }, CancellationToken.None);

        var afterFutureSavingsTransfer = await ReadLoopStateAsync(services);
        AssertLoopState(afterFutureSavingsTransfer, liveBalance: 7425m, planRemaining: 750m, savingsTransfersTotal: 0m);
        Assert.Single(afterFutureSavingsTransfer.Month.SavingsTransfers);

        await services.ExpenseService.CreateMonthSavingsTransferItemAsync(new CreateMonthSavingsTransferItemRequest
        {
            Year = Year,
            Month = Month,
            Amount = 300m,
            TransferDate = new DateOnly(2026, 4, 10)
        }, CancellationToken.None);

        var afterDueSavingsTransfer = await ReadLoopStateAsync(services);
        AssertLoopState(afterDueSavingsTransfer, liveBalance: 7125m, planRemaining: 750m, savingsTransfersTotal: 300m);
        Assert.Equal(2, afterDueSavingsTransfer.Month.SavingsTransfers.Count);

        await services.ExpenseService.CloseMonthAsync(Year, Month, CancellationToken.None);

        var closed = await services.ExpenseService.GetMonthAsync(Year, Month, CancellationToken.None);
        Assert.True(closed.IsClosed);
        await Assert.ThrowsAsync<BadRequestException>(() => services.ExpenseService.UpdateExpenseAsync(new UpdateExpenseRequest
        {
            Id = plannedExpense.Id,
            Name = "Czynsz",
            CategoryId = categoryId,
            PlannedAmount = 1300m,
            ActualAmount = 500m,
            ShowRemainingInUI = true
        }, CancellationToken.None));

        await services.ExpenseService.OpenMonthAsync(Year, Month, CancellationToken.None);
        var reopened = await services.ExpenseService.GetMonthAsync(Year, Month, CancellationToken.None);
        Assert.False(reopened.IsClosed);

        await services.ExpenseService.UpdateExpenseAsync(new UpdateExpenseRequest
        {
            Id = plannedExpense.Id,
            Name = "Czynsz po korekcie",
            CategoryId = categoryId,
            PlannedAmount = 1300m,
            ActualAmount = 500m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        await services.ExpenseService.CloseMonthAsync(Year, Month, CancellationToken.None);

        var finalState = await ReadLoopStateAsync(services);
        Assert.True(finalState.Month.IsClosed);
        AssertLoopState(finalState, liveBalance: 7075m, planRemaining: 800m, savingsTransfersTotal: 300m);
        Assert.Equal(1300m, finalState.Month.Kpi.PlannedTotal);
        Assert.Equal(625m, finalState.Month.Kpi.SpentTotal);
    }

    [Fact]
    public async Task MonthlyBudgetingLoop_Should_Report_Incomplete_When_Previous_Balance_Is_Missing()
    {
        var currentUser = new CurrentUserContext();
        currentUser.SetInteractiveUser(VisibleUserId, User.DefaultUserId);
        var options = NewOptions();
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);
        var services = CreateServices(factory);

        await SeedVisibleProfileOnlyAsync(options, currentUser);
        await using (var context = new ApplicationDbContext(options, currentUser))
        {
            context.Accounts.Add(new Account { Name = "Konto bez marca", Type = (int)AccountType.Bank });
            await context.SaveChangesAsync();
        }

        var liveBalance = await services.IncomeService.GetLiveBalanceAsync(Year, Month, CancellationToken.None);

        Assert.False(liveBalance.HasCompleteBalanceBase);
        Assert.Equal(["Konto bez marca"], liveBalance.MissingBalanceAccountNames);
        Assert.Equal(0m, liveBalance.AccountsBaseTotal);
    }

    [Fact]
    public async Task MonthlyBudgetingLoop_Should_Not_Read_Or_Write_Budget_Without_Interactive_Scope()
    {
        var currentUser = new CurrentUserContext();
        currentUser.SetInteractiveUser(VisibleUserId, User.DefaultUserId);
        var options = NewOptions();

        await SeedVisibleProfileAndFinancialBaseAsync(options, currentUser);

        await using (var noScopeContext = new ApplicationDbContext(options, new CurrentUserContext()))
        {
            Assert.Empty(await noScopeContext.MonthPlans.ToListAsync());
            Assert.Empty(await noScopeContext.Accounts.ToListAsync());

            noScopeContext.MonthPlans.Add(new MonthPlan { Year = Year, Month = Month });
            await Assert.ThrowsAsync<InvalidOperationException>(() => noScopeContext.SaveChangesAsync());
        }
    }

    private static async Task<int> SeedVisibleProfileAndFinancialBaseAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser)
    {
        await SeedVisibleProfileOnlyAsync(options, currentUser);

        await using var context = new ApplicationDbContext(options, currentUser);

        var category = new Category { Name = "Dom", Color = "#43A047" };
        var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
        context.Categories.Add(category);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        context.AccountMonthBalances.Add(new AccountMonthBalance
        {
            AccountId = account.Id,
            Year = 2026,
            Month = 3,
            ClosingBalance = 3000m
        });

        context.Incomes.Add(new Income
        {
            Year = Year,
            Month = Month,
            Name = "Wynagrodzenie",
            Amount = 5000m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 5),
            AccountId = account.Id
        });

        await context.SaveChangesAsync();
        return category.Id;
    }

    private static async Task SeedVisibleProfileOnlyAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser)
    {
        await using var context = new ApplicationDbContext(options, currentUser);
        context.Users.AddRange(
            new User
            {
                Id = User.DefaultUserId,
                Username = User.TechnicalOwnerUsername,
                PasswordHash = string.Empty,
                BudgetOwnerUserId = User.DefaultUserId
            },
            new User
            {
                Id = VisibleUserId,
                Username = "Administrator",
                PasswordHash = PinHasher.Hash(VisibleUserPin),
                BudgetOwnerUserId = User.DefaultUserId,
                HouseholdMode = (int)HouseholdMode.SharedBudget,
                IsAdmin = true
            });

        await context.SaveChangesAsync();
    }

    private static LoopServices CreateServices(IDbContextFactory<ApplicationDbContext> factory)
    {
        var dateTimeProvider = new StaticDateTimeProvider(TodayUtc);
        var expenseService = new ExpenseService(
            factory,
            dateTimeProvider,
            new RecordingAppEventPublisher(),
            new NoOpIncomeService(),
            new NoOpLoanService());
        var incomeService = new IncomeService(factory, dateTimeProvider);

        return new LoopServices(expenseService, incomeService);
    }

    private static async Task<LoopState> ReadLoopStateAsync(LoopServices services)
    {
        var month = await services.ExpenseService.GetMonthAsync(Year, Month, CancellationToken.None);
        var liveBalance = await services.IncomeService.GetLiveBalanceAsync(Year, Month, CancellationToken.None);
        return new LoopState(month, liveBalance);
    }

    private static void AssertLoopState(
        LoopState state,
        decimal liveBalance,
        decimal planRemaining,
        decimal savingsTransfersTotal)
    {
        Assert.True(state.LiveBalance.HasCompleteBalanceBase);
        Assert.Empty(state.LiveBalance.MissingBalanceAccountNames);
        Assert.Equal(liveBalance, state.LiveBalance.CurrentBalance);
        Assert.Equal(planRemaining, state.Month.Kpi.RemainingTotal);
        Assert.Equal(savingsTransfersTotal, state.LiveBalance.SavingsTransfersTotal);
    }

    private static DbContextOptions<ApplicationDbContext> NewOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    private sealed record LoopServices(ExpenseService ExpenseService, IncomeService IncomeService);

    private sealed record LoopState(
        HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto.MonthPlanDto Month,
        HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto.LiveBalanceDto LiveBalance);

    private sealed class ScopedInMemoryDbContextFactory(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUserContext) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options, currentUserContext);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
