using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class AccountServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    
    private AccountService CreateService()
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(DateTime.UtcNow);
        return new AccountService(factory, provider);
    }
    
    [Fact]
    public async Task CreateAndUpsertMonthBalance_Should_Update_CurrentBalance()
    {
        var service = CreateService();
        var now = DateTime.UtcNow;

        var created = await service.CreateAccountAsync(new CreateAccountRequest
        {
            Name = "Bank główny",
            Type = AccountType.Bank,
            ClosingBalance = 1000m
        }, CancellationToken.None);

        await service.UpsertMonthBalanceAsync(new UpsertAccountMonthBalanceRequest
        {
            AccountId = created.Id,
            Year = now.Year,
            Month = now.Month,
            ClosingBalance = 1200m
        }, CancellationToken.None);

        var all = await service.GetAllAsync(CancellationToken.None);
        var account = Assert.Single(all);

        Assert.Equal(1200m, account.CurrentBalance);
        Assert.Single(account.MonthBalances);
    }

    [Fact]
    public async Task SetAccountArchivedAsync_Should_Set_Archive_Flag()
    {
        int accountId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account
            {
                Name = "Portfel",
                Type = (int)AccountType.Cash
            };

            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        await service.SetAccountArchivedAsync(new SetAccountArchivedRequest { Id = accountId, IsArchived = true }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var archived = await verifyContext.Accounts.IgnoreQueryFilters().SingleAsync(x => x.Id == accountId);
        Assert.True(archived.IsArchived);
    }

    [Fact]
    public async Task UpsertMonthBalanceAsync_Should_Throw_When_Month_Is_Closed()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account
            {
                Name = "Bank zamkniety",
                Type = (int)AccountType.Bank
            };

            context.Accounts.Add(account);
            context.MonthPlans.Add(new MonthPlan
            {
                Year = 2027,
                Month = 11,
                IsClosed = true
            });

            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();

        var action = () => service.UpsertMonthBalanceAsync(new UpsertAccountMonthBalanceRequest
        {
            AccountId = accountId,
            Year = 2027,
            Month = 11,
            ClosingBalance = 1500m
        }, CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(action);
    }

    [Fact]
    public async Task UpdateMonthBalanceAmountAsync_Should_Throw_When_Month_Is_Closed()
    {
        int monthBalanceId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account
            {
                Name = "Bank arch",
                Type = (int)AccountType.Bank
            };

            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            var monthBalance = new AccountMonthBalance
            {
                AccountId = account.Id,
                Year = 2026,
                Month = 5,
                ClosingBalance = 1000m
            };

            context.AccountMonthBalances.Add(monthBalance);
            context.MonthPlans.Add(new MonthPlan
            {
                Year = 2026,
                Month = 5,
                IsClosed = true
            });

            await context.SaveChangesAsync();
            monthBalanceId = monthBalance.Id;
        }

        var service = CreateService();

        var action = () => service.UpdateMonthBalanceAmountAsync(new UpdateAccountMonthBalanceAmountRequest
        {
            Id = monthBalanceId,
            ClosingBalance = 1300m
        }, CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(action);
    }
}

