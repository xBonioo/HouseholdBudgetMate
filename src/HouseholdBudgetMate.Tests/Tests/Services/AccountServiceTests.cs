using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
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

        var created = await service.CreateAccountAsync(new CreateAccountRequest
        {
            Name = "Bank główny",
            Type = AccountType.Bank,
            ClosingBalance = 1000m
        }, CancellationToken.None);

        await service.UpsertMonthBalanceAsync(new UpsertAccountMonthBalanceRequest
        {
            AccountId = created.Id,
            Year = 2026,
            Month = 4,
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
}

