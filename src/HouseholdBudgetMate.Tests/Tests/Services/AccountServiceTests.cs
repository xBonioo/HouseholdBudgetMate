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
    private static readonly DateTime DefaultNowUtc = new(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);

    private AccountService CreateService(DateTime? nowUtc = null)
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(nowUtc ?? DefaultNowUtc);
        return new AccountService(factory, provider);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that GetAllAsync returns accounts ordered by Order then by Name.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_Should_Return_Accounts_Ordered_By_Order_Then_Name()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Accounts.AddRange(
                new Account { Name = "Zebra", Order = 2, Type = (int)AccountType.Bank },
                new Account { Name = "Apple", Order = 2, Type = (int)AccountType.Bank },
                new Account { Name = "Main", Order = 1, Type = (int)AccountType.Cash });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal("Main", result[0].Name);
        Assert.Equal("Apple", result[1].Name);
        Assert.Equal("Zebra", result[2].Name);
    }

    /// <summary>
    /// Verifies that GetAllAsync includes archived accounts in the result.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_Should_Include_Archived_Accounts()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Accounts.AddRange(
                new Account { Name = "Active", Order = 1, Type = (int)AccountType.Bank },
                new Account { Name = "Archived", Order = 2, Type = (int)AccountType.Cash, IsArchived = true, ArchivedAtUtc = DefaultNowUtc });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    /// <summary>
    /// Verifies that GetAllAsync returns an empty list when there are no accounts.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_Should_Return_Empty_List_When_No_Accounts()
    {
        var service = CreateService();
        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    // ── CreateAccountAsync ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies that CreateAccountAsync persists all fields and creates the initial month balance for the current month.
    /// </summary>
    [Fact]
    public async Task CreateAccountAsync_Should_Persist_Fields_And_Create_Initial_Month_Balance()
    {
        var service = CreateService();
        var result = await service.CreateAccountAsync(new CreateAccountRequest
        {
            Name = "Savings Account",
            Type = AccountType.Savings,
            ClosingBalance = 5000m
        }, CancellationToken.None);

        Assert.Equal("Savings Account", result.Name);
        Assert.Equal(AccountType.Savings, result.Type);
        Assert.Single(result.MonthBalances);
        Assert.Equal(5000m, result.MonthBalances[0].ClosingBalance);
        Assert.Equal(DefaultNowUtc.Year, result.MonthBalances[0].Year);
        Assert.Equal(DefaultNowUtc.Month, result.MonthBalances[0].Month);
    }

    /// <summary>
    /// Verifies that CreateAccountAsync assigns sequential Order values starting from 1.
    /// </summary>
    [Fact]
    public async Task CreateAccountAsync_Should_Assign_Sequential_Order()
    {
        var service = CreateService();

        var first = await service.CreateAccountAsync(new CreateAccountRequest
        {
            Name = "First",
            Type = AccountType.Cash,
            ClosingBalance = 0m
        }, CancellationToken.None);

        var second = await service.CreateAccountAsync(new CreateAccountRequest
        {
            Name = "Second",
            Type = AccountType.Bank,
            ClosingBalance = 0m
        }, CancellationToken.None);

        Assert.Equal(1, first.Order);
        Assert.Equal(2, second.Order);
    }

    /// <summary>
    /// Verifies that CreateAccountAsync updates CurrentBalance when a new month balance is added via UpsertMonthBalanceAsync.
    /// </summary>
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

        // Upsert a balance for a later month so it becomes the CurrentBalance
        await service.UpsertMonthBalanceAsync(new UpsertAccountMonthBalanceRequest
        {
            AccountId = created.Id,
            Year = DefaultNowUtc.Year,
            Month = DefaultNowUtc.Month,
            ClosingBalance = 1200m
        }, CancellationToken.None);

        var all = await service.GetAllAsync(CancellationToken.None);
        var account = Assert.Single(all);

        Assert.Equal(1200m, account.CurrentBalance);
        Assert.Single(account.MonthBalances);
    }

    /// <summary>
    /// Verifies that CreateAccountAsync throws ConflictException when the name is already taken (case-insensitive).
    /// </summary>
    [Fact]
    public async Task CreateAccountAsync_Should_Throw_Conflict_When_Name_Is_Duplicate()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Accounts.Add(new Account { Name = "Main Bank", Order = 1, Type = (int)AccountType.Bank });
            await context.SaveChangesAsync();
        }

        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAccountAsync(new CreateAccountRequest
        {
            Name = "  main bank  ",
            Type = AccountType.Cash,
            ClosingBalance = 0m
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that CreateAccountAsync throws BadRequestException when Name is empty.
    /// </summary>
    [Fact]
    public async Task CreateAccountAsync_Should_Throw_BadRequest_When_Name_Is_Empty()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAccountAsync(new CreateAccountRequest
        {
            Name = "   ",
            Type = AccountType.Bank,
            ClosingBalance = 0m
        }, CancellationToken.None));
    }

    // ── UpdateAccountAsync ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies that UpdateAccountAsync persists updated Name and Type fields.
    /// </summary>
    [Fact]
    public async Task UpdateAccountAsync_Should_Persist_Updated_Fields()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "OldName", Order = 1, Type = (int)AccountType.Cash };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        var result = await service.UpdateAccountAsync(new UpdateAccountRequest
        {
            Id = accountId,
            Name = "NewName",
            Type = AccountType.Savings
        }, CancellationToken.None);

        Assert.Equal("NewName", result.Name);
        Assert.Equal(AccountType.Savings, result.Type);
    }

    /// <summary>
    /// Verifies that UpdateAccountAsync throws NotFoundException when the account does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateAccountAsync_Should_Throw_NotFoundException_When_Account_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAccountAsync(new UpdateAccountRequest
        {
            Id = 9999,
            Name = "Ghost",
            Type = AccountType.Bank
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that UpdateAccountAsync allows keeping the same name (exclude-self logic in duplicate check).
    /// </summary>
    [Fact]
    public async Task UpdateAccountAsync_Should_Allow_Keeping_Same_Name()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Portfel", Order = 1, Type = (int)AccountType.Cash };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        var result = await service.UpdateAccountAsync(new UpdateAccountRequest
        {
            Id = accountId,
            Name = "Portfel",
            Type = AccountType.Bank
        }, CancellationToken.None);

        Assert.Equal("Portfel", result.Name);
        Assert.Equal(AccountType.Bank, result.Type);
    }

    /// <summary>
    /// Verifies that UpdateAccountAsync throws ConflictException when the new name is already taken by another account.
    /// </summary>
    [Fact]
    public async Task UpdateAccountAsync_Should_Throw_Conflict_When_Name_Taken_By_Another()
    {
        int accountIdToUpdate;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Accounts.AddRange(
                new Account { Name = "BankA", Order = 1, Type = (int)AccountType.Bank },
                new Account { Name = "BankB", Order = 2, Type = (int)AccountType.Bank });
            await context.SaveChangesAsync();

            accountIdToUpdate = (await context.Accounts.SingleAsync(x => x.Name == "BankB")).Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAccountAsync(new UpdateAccountRequest
        {
            Id = accountIdToUpdate,
            Name = "BankA",
            Type = AccountType.Bank
        }, CancellationToken.None));
    }

    // ── DeleteAccountAsync ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies that DeleteAccountAsync hard-deletes the account and all its month balances.
    /// </summary>
    [Fact]
    public async Task DeleteAccountAsync_Should_Delete_Account_And_MonthBalances()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "ToDelete", Order = 1, Type = (int)AccountType.Cash };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance { AccountId = accountId, Year = 2026, Month = 1, ClosingBalance = 100m },
                new AccountMonthBalance { AccountId = accountId, Year = 2026, Month = 2, ClosingBalance = 200m });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        await service.DeleteAccountAsync(new DeleteAccountRequest { Id = accountId }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var accountExists = await verifyContext.Accounts.AnyAsync(x => x.Id == accountId);
        var balancesExist = await verifyContext.AccountMonthBalances.AnyAsync(x => x.AccountId == accountId);

        Assert.False(accountExists);
        Assert.False(balancesExist);
    }

    /// <summary>
    /// Verifies that DeleteAccountAsync throws NotFoundException when the account does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteAccountAsync_Should_Throw_NotFoundException_When_Account_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteAccountAsync(new DeleteAccountRequest { Id = 9999 }, CancellationToken.None));
    }

    // ── SetAccountArchivedAsync ──────────────────────────────────────────────

    /// <summary>
    /// Verifies that SetAccountArchivedAsync sets IsArchived=true and records the ArchivedAtUtc timestamp.
    /// </summary>
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
        Assert.Equal(DefaultNowUtc, archived.ArchivedAtUtc);
    }

    /// <summary>
    /// Verifies that SetAccountArchivedAsync clears IsArchived and nulls ArchivedAtUtc when IsArchived=false.
    /// </summary>
    [Fact]
    public async Task SetAccountArchivedAsync_Should_Clear_Archive_Flag()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account
            {
                Name = "Portfel",
                Type = (int)AccountType.Cash,
                IsArchived = true,
                ArchivedAtUtc = DefaultNowUtc.AddDays(-10)
            };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        await service.SetAccountArchivedAsync(new SetAccountArchivedRequest { Id = accountId, IsArchived = false }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var verified = await verifyContext.Accounts.SingleAsync(x => x.Id == accountId);

        Assert.False(verified.IsArchived);
        Assert.Null(verified.ArchivedAtUtc);
    }

    /// <summary>
    /// Verifies that SetAccountArchivedAsync throws NotFoundException when the account does not exist.
    /// </summary>
    [Fact]
    public async Task SetAccountArchivedAsync_Should_Throw_NotFoundException_When_Account_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SetAccountArchivedAsync(new SetAccountArchivedRequest { Id = 9999, IsArchived = true }, CancellationToken.None));
    }

    // ── ReorderAccountsAsync ─────────────────────────────────────────────────

    /// <summary>
    /// Verifies that ReorderAccountsAsync assigns Order values matching the requested sequence.
    /// </summary>
    [Fact]
    public async Task ReorderAccountsAsync_Should_Update_Orders_Per_Requested_Sequence()
    {
        int id1;
        int id2;
        int id3;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var a = new Account { Name = "A", Order = 1, Type = (int)AccountType.Cash };
            var b = new Account { Name = "B", Order = 2, Type = (int)AccountType.Bank };
            var c = new Account { Name = "C", Order = 3, Type = (int)AccountType.Savings };
            context.Accounts.AddRange(a, b, c);
            await context.SaveChangesAsync();
            id1 = a.Id;
            id2 = b.Id;
            id3 = c.Id;
        }

        var service = CreateService();
        // Reverse the order: C=1, A=2, B=3
        await service.ReorderAccountsAsync(new ReorderAccountsRequest
        {
            AccountIds = [id3, id1, id2]
        }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var accounts = await verifyContext.Accounts.ToListAsync();

        Assert.Equal(2, accounts.Single(x => x.Id == id1).Order);
        Assert.Equal(3, accounts.Single(x => x.Id == id2).Order);
        Assert.Equal(1, accounts.Single(x => x.Id == id3).Order);
    }

    /// <summary>
    /// Verifies that ReorderAccountsAsync throws BadRequestException when some account IDs are not found.
    /// </summary>
    [Fact]
    public async Task ReorderAccountsAsync_Should_Throw_BadRequest_When_Some_Accounts_Not_Found()
    {
        int existingId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Existing", Order = 1, Type = (int)AccountType.Cash };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            existingId = account.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.ReorderAccountsAsync(new ReorderAccountsRequest
        {
            AccountIds = [existingId, 9999]
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that ReorderAccountsAsync does nothing and returns successfully when the list is empty.
    /// </summary>
    [Fact]
    public async Task ReorderAccountsAsync_Should_Do_Nothing_When_List_Is_Empty()
    {
        var service = CreateService();

        // Should not throw
        await service.ReorderAccountsAsync(new ReorderAccountsRequest { AccountIds = [] }, CancellationToken.None);
    }

    // ── UpsertMonthBalanceAsync ──────────────────────────────────────────────

    /// <summary>
    /// Verifies that UpsertMonthBalanceAsync creates a new balance record when none exists for the month.
    /// </summary>
    [Fact]
    public async Task UpsertMonthBalanceAsync_Should_Create_Balance_When_Not_Exists()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Bank", Order = 1, Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        var result = await service.UpsertMonthBalanceAsync(new UpsertAccountMonthBalanceRequest
        {
            AccountId = accountId,
            Year = 2026,
            Month = 3,
            ClosingBalance = 999m
        }, CancellationToken.None);

        Assert.Equal(accountId, result.AccountId);
        Assert.Equal(2026, result.Year);
        Assert.Equal(3, result.Month);
        Assert.Equal(999m, result.ClosingBalance);
    }

    /// <summary>
    /// Verifies that UpsertMonthBalanceAsync updates the ClosingBalance when a record already exists for that month.
    /// </summary>
    [Fact]
    public async Task UpsertMonthBalanceAsync_Should_Update_Balance_When_Already_Exists()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Bank", Order = 1, Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;

            context.AccountMonthBalances.Add(new AccountMonthBalance
            {
                AccountId = accountId,
                Year = 2026,
                Month = 4,
                ClosingBalance = 500m
            });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.UpsertMonthBalanceAsync(new UpsertAccountMonthBalanceRequest
        {
            AccountId = accountId,
            Year = 2026,
            Month = 4,
            ClosingBalance = 750m
        }, CancellationToken.None);

        Assert.Equal(750m, result.ClosingBalance);

        // Only one balance record should exist for that month
        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var count = await verifyContext.AccountMonthBalances.CountAsync(x => x.AccountId == accountId && x.Year == 2026 && x.Month == 4);
        Assert.Equal(1, count);
    }

    /// <summary>
    /// Verifies that UpsertMonthBalanceAsync throws BadRequestException when the month is closed.
    /// </summary>
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

    /// <summary>
    /// Verifies that UpsertMonthBalanceAsync throws NotFoundException when the account does not exist.
    /// </summary>
    [Fact]
    public async Task UpsertMonthBalanceAsync_Should_Throw_NotFoundException_When_Account_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpsertMonthBalanceAsync(new UpsertAccountMonthBalanceRequest
        {
            AccountId = 9999,
            Year = 2026,
            Month = 5,
            ClosingBalance = 100m
        }, CancellationToken.None));
    }

    // ── UpdateMonthBalanceAmountAsync ────────────────────────────────────────

    /// <summary>
    /// Verifies that UpdateMonthBalanceAmountAsync persists the updated ClosingBalance.
    /// </summary>
    [Fact]
    public async Task UpdateMonthBalanceAmountAsync_Should_Update_Balance()
    {
        int balanceId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Bank", Order = 1, Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            var balance = new AccountMonthBalance
            {
                AccountId = account.Id,
                Year = 2026,
                Month = 3,
                ClosingBalance = 300m
            };
            context.AccountMonthBalances.Add(balance);
            await context.SaveChangesAsync();
            balanceId = balance.Id;
        }

        var service = CreateService();
        var result = await service.UpdateMonthBalanceAmountAsync(new UpdateAccountMonthBalanceAmountRequest
        {
            Id = balanceId,
            ClosingBalance = 450m
        }, CancellationToken.None);

        Assert.Equal(450m, result.ClosingBalance);
    }

    /// <summary>
    /// Verifies that UpdateMonthBalanceAmountAsync throws BadRequestException when the month is closed.
    /// </summary>
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

    /// <summary>
    /// Verifies that UpdateMonthBalanceAmountAsync throws NotFoundException when the balance record does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateMonthBalanceAmountAsync_Should_Throw_NotFoundException_When_Balance_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateMonthBalanceAmountAsync(
            new UpdateAccountMonthBalanceAmountRequest { Id = 9999, ClosingBalance = 100m },
            CancellationToken.None));
    }
}

