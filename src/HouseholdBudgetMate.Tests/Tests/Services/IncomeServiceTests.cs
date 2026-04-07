using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;
namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class IncomeServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private static readonly DateTime DefaultNowUtc = new(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

    private IncomeService CreateService(DateTime? nowUtc = null)
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(nowUtc ?? DefaultNowUtc);
        return new IncomeService(factory, provider);
    }

    [Fact]
    public async Task CreateAndGetMonthIncomes_Should_Return_Income_For_Selected_Month()
    {
        int accountId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();

        await service.CreateIncomeAsync(new CreateIncomeRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Wypłata",
            Amount = 5000m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 10),
            AccountId = accountId
        }, CancellationToken.None);

        var incomes = await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None);

        var income = Assert.Single(incomes);
        Assert.Equal("Wypłata", income.Name);
        Assert.Equal(5000m, income.Amount);
    }

    [Fact]
    public async Task GetLiveBalanceAsync_Should_Use_Formula_Accounts_Plus_Incomes_Minus_Expenses_For_Already_Due_Incomes()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            var savings = new Account { Name = "Poduszka", Type = (int)AccountType.Savings };
            var category = new Category { Name = "Transport", Color = "#123456" };
            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };

            context.Accounts.AddRange(account, savings);
            context.Categories.Add(category);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance { AccountId = account.Id, Year = 2026, Month = 3, ClosingBalance = 2000m },
                new AccountMonthBalance { AccountId = savings.Id, Year = 2026, Month = 3, ClosingBalance = 7000m });

            context.Incomes.Add(new Income
            {
                Year = 2026,
                Month = 4,
                Name = "Premia",
                Amount = 500m,
                ExpectedDayOfMonth = new DateOnly(2026, 4, 5),
                AccountId = account.Id
            });

            context.Expenses.Add(new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Paliwo",
                CategoryId = category.Id,
                PlannedAmount = 300m,
                ActualAmount = 250m
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.Equal(2000m, liveBalance.AccountsBaseTotal);
        Assert.Equal(500m, liveBalance.IncomesTotal);
        Assert.Equal(250m, liveBalance.ExpensesTotal);
        Assert.Equal(2250m, liveBalance.CurrentBalance);
    }

    [Fact]
    public async Task GetLiveBalanceAsync_Should_Ignore_Future_Dated_Incomes_In_Current_Month()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            var category = new Category { Name = "Dom", Color = "#123456" };
            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };

            context.Accounts.Add(account);
            context.Categories.Add(category);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.Add(new AccountMonthBalance
            {
                AccountId = account.Id,
                Year = 2026,
                Month = 3,
                ClosingBalance = 1000m
            });

            context.Incomes.AddRange(
                new Income
                {
                    Year = 2026,
                    Month = 4,
                    Name = "Wplyw dzisiaj",
                    Amount = 200m,
                    ExpectedDayOfMonth = new DateOnly(2026, 4, 10),
                    AccountId = account.Id
                },
                new Income
                {
                    Year = 2026,
                    Month = 4,
                    Name = "Wplyw pozniej",
                    Amount = 500m,
                    ExpectedDayOfMonth = new DateOnly(2026, 4, 20),
                    AccountId = account.Id
                });

            context.Expenses.Add(new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Rachunek",
                CategoryId = category.Id,
                PlannedAmount = 300m,
                ActualAmount = 100m
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.Equal(200m, liveBalance.IncomesTotal);
        Assert.Equal(1100m, liveBalance.CurrentBalance);
    }

    [Fact]
    public async Task SyncRegularIncomesForMonthAsync_Should_Create_Regular_Incomes_And_Be_Idempotent()
    {
        int accountId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();

        await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);

        await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Premia kwartalna",
            Amount = 1200m,
            DayOfMonth = 20,
            AccountId = accountId
        }, CancellationToken.None);

        await service.SyncRegularIncomesForMonthAsync(2026, 4, CancellationToken.None);
        await service.SyncRegularIncomesForMonthAsync(2026, 4, CancellationToken.None);

        var incomes = await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None);

        Assert.Equal(2, incomes.Count);
        Assert.All(incomes, x => Assert.True(x.IsRegular));
    }

    [Fact]
    public async Task GetMonthIncomesAsync_Should_Return_Regular_And_Manual_Incomes_Together()
    {
        int accountId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();

        await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);

        await service.SyncRegularIncomesForMonthAsync(2026, 4, CancellationToken.None);

        await service.CreateIncomeAsync(new CreateIncomeRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Premia",
            Amount = 700m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 10),
            AccountId = accountId,
            IsRegular = false
        }, CancellationToken.None);

        var incomes = await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None);

        Assert.Equal(2, incomes.Count);
        Assert.Contains(incomes, x => x.IsRegular && x.Name == "Wyplata");
        Assert.Contains(incomes, x => !x.IsRegular && x.Name == "Premia");
    }

    [Fact]
    public async Task GetLiveBalanceAsync_Should_Subtract_Savings_Transfer_When_Due_Date_Reached()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            var category = new Category { Name = "Dom", Color = "#123456" };
            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };

            context.Accounts.Add(account);
            context.Categories.Add(category);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.Add(new AccountMonthBalance
            {
                AccountId = account.Id,
                Year = 2026,
                Month = 3,
                ClosingBalance = 2000m
            });

            context.Incomes.Add(new Income
            {
                Year = 2026,
                Month = 4,
                Name = "Wyplata",
                Amount = 500m,
                ExpectedDayOfMonth = new DateOnly(2026, 4, 10),
                AccountId = account.Id
            });

            context.Expenses.Add(new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Koszt",
                CategoryId = category.Id,
                PlannedAmount = 300m,
                ActualAmount = 100m
            });

            context.MonthSavingsTransferItems.Add(new MonthSavingsTransferItem
            {
                MonthPlanId = monthPlan.Id,
                Amount = 300m,
                TransferDate = new DateOnly(2026, 4, 10)
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.Equal(300m, liveBalance.SavingsTransfersTotal);
        Assert.Equal(2100m, liveBalance.CurrentBalance);
    }

    [Fact]
    public async Task CreateIncomeAsync_Should_Throw_When_Month_Is_Closed()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 8, IsClosed = true });
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateIncomeAsync(new CreateIncomeRequest
        {
            Year = 2026,
            Month = 8,
            Name = "Wplyw",
            Amount = 1000m,
            ExpectedDayOfMonth = new DateOnly(2026, 8, 10),
            AccountId = accountId,
            IsRegular = false
        }, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteRegularDefinitionAsync_Should_SoftDelete_By_Setting_IsActive_False()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        var created = await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);

        await service.DeleteRegularDefinitionAsync(new DeleteRegularIncomeDefinitionRequest { Id = created.Id }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var definition = await verifyContext.RegularIncomeDefinitions.FirstAsync(x => x.Id == created.Id);
        Assert.False(definition.IsActive);
    }

    [Fact]
    public async Task DeleteRegularIncomeOccurrence_Should_Remove_Only_Current_Month_And_Keep_Future_Generation()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);

        await service.SyncRegularIncomesForMonthAsync(2026, 4, CancellationToken.None);
        var april = await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None);
        var regularIncome = Assert.Single(april, x => x.IsRegular);

        await service.DeleteIncomeAsync(new DeleteIncomeRequest { Id = regularIncome.Id }, CancellationToken.None);
        await service.SyncRegularIncomesForMonthAsync(2026, 4, CancellationToken.None);

        var aprilAfterDelete = await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None);
        Assert.DoesNotContain(aprilAfterDelete, x => x.IsRegular && x.Name == "Wyplata");

        await service.SyncRegularIncomesForMonthAsync(2026, 5, CancellationToken.None);
        var may = await service.GetMonthIncomesAsync(2026, 5, CancellationToken.None);
        Assert.Contains(may, x => x.IsRegular && x.Name == "Wyplata");
    }

    [Fact]
    public async Task DeleteRegularDefinitionPermanentlyAsync_Should_Remove_Definition_From_Database()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        var created = await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);

        await service.DeleteRegularDefinitionPermanentlyAsync(
            new DeleteRegularIncomeDefinitionRequest { Id = created.Id },
            CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var exists = await verifyContext.RegularIncomeDefinitions.AnyAsync(x => x.Id == created.Id);
        Assert.False(exists);
    }
}

