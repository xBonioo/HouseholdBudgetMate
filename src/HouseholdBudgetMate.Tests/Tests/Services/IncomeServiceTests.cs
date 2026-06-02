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

    // DefaultNowUtc = 10 April 2026 — used as "today" in live balance tests.
    // Incomes with ExpectedDayOfMonth <= 10 Apr are counted; later dates are ignored.
    private static readonly DateTime DefaultNowUtc = new(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

    private IncomeService CreateService(DateTime? nowUtc = null)
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(nowUtc ?? DefaultNowUtc);
        return new IncomeService(factory, provider);
    }

    // -------------------------------------------------------------------------
    // CreateIncomeAsync / GetMonthIncomesAsync — happy path
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies the basic scenario: an income saved via CreateIncomeAsync
    /// is visible in GetMonthIncomesAsync for the same month.
    /// </summary>
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

    /// <summary>
    /// CreateIncomeAsync throws NotFoundException when the account with the given AccountId does not exist.
    /// </summary>
    [Fact]
    public async Task CreateIncomeAsync_Should_Throw_When_Account_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateIncomeAsync(new CreateIncomeRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Wplyw",
            Amount = 1000m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 10),
            AccountId = 9999
        }, CancellationToken.None));
    }

    /// <summary>
    /// CreateIncomeAsync throws BadRequestException when the month is closed (IsClosed=true).
    /// Domain rule: a closed month is read-only.
    /// </summary>
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

    // -------------------------------------------------------------------------
    // UpdateIncomeAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// UpdateIncomeAsync updates the Name, Amount, ExpectedDayOfMonth, AccountId and IsRegular fields
    /// and returns the updated DTO.
    /// </summary>
    [Fact]
    public async Task UpdateIncomeAsync_Should_Update_Income_Fields()
    {
        int accountId;
        int income2AccountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account1 = new Account { Name = "Konto1", Type = (int)AccountType.Bank };
            var account2 = new Account { Name = "Konto2", Type = (int)AccountType.Bank };
            context.Accounts.AddRange(account1, account2);
            await context.SaveChangesAsync();
            accountId = account1.Id;
            income2AccountId = account2.Id;
        }

        var service = CreateService();
        var created = await service.CreateIncomeAsync(new CreateIncomeRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Stara nazwa",
            Amount = 1000m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 5),
            AccountId = accountId
        }, CancellationToken.None);

        var updated = await service.UpdateIncomeAsync(new UpdateIncomeRequest
        {
            Id = created.Id,
            Name = "Nowa nazwa",
            Amount = 2500m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 15),
            AccountId = income2AccountId,
            IsRegular = false
        }, CancellationToken.None);

        Assert.Equal("Nowa nazwa", updated.Name);
        Assert.Equal(2500m, updated.Amount);
        Assert.Equal(new DateOnly(2026, 4, 15), updated.ExpectedDayOfMonth);
        Assert.Equal(income2AccountId, updated.AccountId);
    }

    /// <summary>
    /// UpdateIncomeAsync throws NotFoundException when an income with the given Id does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateIncomeAsync_Should_Throw_When_Income_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateIncomeAsync(new UpdateIncomeRequest
        {
            Id = 9999,
            Name = "Nieistniejący",
            Amount = 100m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 10),
            AccountId = 1
        }, CancellationToken.None));
    }

    /// <summary>
    /// UpdateIncomeAsync throws BadRequestException when the income's month is closed.
    /// </summary>
    [Fact]
    public async Task UpdateIncomeAsync_Should_Throw_When_Month_Is_Closed()
    {
        int accountId;
        int incomeId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            var monthPlan = new MonthPlan { Year = 2026, Month = 6, IsClosed = true };
            context.Accounts.Add(account);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();
            accountId = account.Id;

            var income = new Income
            {
                Year = 2026,
                Month = 6,
                Name = "Stary wpływ",
                Amount = 1000m,
                ExpectedDayOfMonth = new DateOnly(2026, 6, 10),
                AccountId = account.Id
            };
            context.Incomes.Add(income);
            await context.SaveChangesAsync();
            incomeId = income.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateIncomeAsync(new UpdateIncomeRequest
        {
            Id = incomeId,
            Name = "Zmiana",
            Amount = 1500m,
            ExpectedDayOfMonth = new DateOnly(2026, 6, 15),
            AccountId = accountId
        }, CancellationToken.None));
    }

    /// <summary>
    /// UpdateIncomeAsync throws BadRequestException when the new ExpectedDayOfMonth
    /// does not belong to the income's month (date-month consistency validation).
    /// </summary>
    [Fact]
    public async Task UpdateIncomeAsync_Should_Throw_When_Date_Does_Not_Belong_To_Income_Month()
    {
        int accountId;
        int incomeId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;

            var income = new Income
            {
                Year = 2026,
                Month = 4,
                Name = "Wpływ",
                Amount = 1000m,
                ExpectedDayOfMonth = new DateOnly(2026, 4, 10),
                AccountId = account.Id
            };
            context.Incomes.Add(income);
            await context.SaveChangesAsync();
            incomeId = income.Id;
        }

        var service = CreateService();

        // May date — does not belong to month 4
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateIncomeAsync(new UpdateIncomeRequest
        {
            Id = incomeId,
            Name = "Wpływ",
            Amount = 1000m,
            ExpectedDayOfMonth = new DateOnly(2026, 5, 10),
            AccountId = accountId
        }, CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // DeleteIncomeAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// DeleteIncomeAsync sets IsDeleted=true (soft delete) — the income disappears from GetMonthIncomesAsync
    /// but remains in the database with the IsDeleted flag set.
    /// </summary>
    [Fact]
    public async Task DeleteIncomeAsync_Should_SoftDelete_Income()
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
        var created = await service.CreateIncomeAsync(new CreateIncomeRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Wpływ",
            Amount = 1000m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 10),
            AccountId = accountId
        }, CancellationToken.None);

        await service.DeleteIncomeAsync(new DeleteIncomeRequest { Id = created.Id }, CancellationToken.None);

        // Service view: income is no longer visible
        var incomes = await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None);
        Assert.Empty(incomes);

        // Database verification: record still exists with IsDeleted=true
        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var income = await verifyContext.Incomes.IgnoreQueryFilters().FirstAsync(x => x.Id == created.Id);
        Assert.True(income.IsDeleted);
        Assert.NotNull(income.DeletedAtUtc);
    }

    /// <summary>
    /// DeleteIncomeAsync throws NotFoundException when an income with the given Id does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteIncomeAsync_Should_Throw_When_Income_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteIncomeAsync(new DeleteIncomeRequest { Id = 9999 }, CancellationToken.None));
    }

    /// <summary>
    /// DeleteIncomeAsync throws BadRequestException when the income's month is closed.
    /// </summary>
    [Fact]
    public async Task DeleteIncomeAsync_Should_Throw_When_Month_Is_Closed()
    {
        int incomeId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            var monthPlan = new MonthPlan { Year = 2026, Month = 5, IsClosed = true };
            context.Accounts.Add(account);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var income = new Income
            {
                Year = 2026,
                Month = 5,
                Name = "Wpływ",
                Amount = 500m,
                ExpectedDayOfMonth = new DateOnly(2026, 5, 5),
                AccountId = account.Id
            };
            context.Incomes.Add(income);
            await context.SaveChangesAsync();
            incomeId = income.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.DeleteIncomeAsync(new DeleteIncomeRequest { Id = incomeId }, CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // GetMonthIncomesAsync — edge cases
    // -------------------------------------------------------------------------

    /// <summary>
    /// GetMonthIncomesAsync returns an empty list when there are no incomes in the given month.
    /// </summary>
    [Fact]
    public async Task GetMonthIncomesAsync_Should_Return_Empty_When_No_Incomes_In_Month()
    {
        var service = CreateService();

        var incomes = await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None);

        Assert.Empty(incomes);
    }

    /// <summary>
    /// GetMonthIncomesAsync returns regular and manual incomes together,
    /// sorted ascending by ExpectedDayOfMonth.
    /// </summary>
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

    // -------------------------------------------------------------------------
    // GetRegularDefinitionsAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// GetRegularDefinitionsAsync returns all definitions (active and inactive)
    /// sorted alphabetically by name.
    /// </summary>
    [Fact]
    public async Task GetRegularDefinitionsAsync_Should_Return_All_Definitions_Sorted_By_Name()
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
            Name = "Zysk",
            Amount = 200m,
            DayOfMonth = 5,
            AccountId = accountId
        }, CancellationToken.None);

        await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Abonament",
            Amount = 50m,
            DayOfMonth = 1,
            AccountId = accountId
        }, CancellationToken.None);

        var definitions = await service.GetRegularDefinitionsAsync(CancellationToken.None);

        Assert.Equal(2, definitions.Count);
        Assert.Equal("Abonament", definitions[0].Name);
        Assert.Equal("Zysk", definitions[1].Name);
    }

    // -------------------------------------------------------------------------
    // CreateRegularDefinitionAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// CreateRegularDefinitionAsync throws NotFoundException when the account with the given AccountId does not exist.
    /// </summary>
    [Fact]
    public async Task CreateRegularDefinitionAsync_Should_Throw_When_Account_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateRegularDefinitionAsync(
            new CreateRegularIncomeDefinitionRequest
            {
                Name = "Wyplata",
                Amount = 5000m,
                DayOfMonth = 10,
                AccountId = 9999
            }, CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // UpdateRegularDefinitionAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// UpdateRegularDefinitionAsync updates Name, Amount, DayOfMonth, AccountId and IsActive
    /// and returns the updated DTO.
    /// </summary>
    [Fact]
    public async Task UpdateRegularDefinitionAsync_Should_Update_Definition_Fields()
    {
        int accountId;
        int account2Id;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account1 = new Account { Name = "Konto1", Type = (int)AccountType.Bank };
            var account2 = new Account { Name = "Konto2", Type = (int)AccountType.Bank };
            context.Accounts.AddRange(account1, account2);
            await context.SaveChangesAsync();
            accountId = account1.Id;
            account2Id = account2.Id;
        }

        var service = CreateService();
        var created = await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Stara",
            Amount = 1000m,
            DayOfMonth = 5,
            AccountId = accountId
        }, CancellationToken.None);

        var updated = await service.UpdateRegularDefinitionAsync(new UpdateRegularIncomeDefinitionRequest
        {
            Id = created.Id,
            Name = "Nowa",
            Amount = 2000m,
            DayOfMonth = 15,
            AccountId = account2Id,
            IsActive = true
        }, CancellationToken.None);

        Assert.Equal("Nowa", updated.Name);
        Assert.Equal(2000m, updated.Amount);
        Assert.Equal(15, updated.DayOfMonth);
        Assert.Equal(account2Id, updated.AccountId);
    }

    /// <summary>
    /// UpdateRegularDefinitionAsync throws NotFoundException when a definition with the given Id does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateRegularDefinitionAsync_Should_Throw_When_Definition_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateRegularDefinitionAsync(
            new UpdateRegularIncomeDefinitionRequest
            {
                Id = 9999,
                Name = "Brak",
                Amount = 100m,
                DayOfMonth = 10,
                AccountId = 1
            }, CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // DeleteRegularDefinitionAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// DeleteRegularDefinitionAsync sets IsActive=false (soft delete).
    /// The definition remains in the database but is marked as inactive.
    /// </summary>
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

    /// <summary>
    /// DeleteRegularDefinitionAsync is idempotent — calling it on an already inactive definition
    /// does not throw and does not change state.
    /// </summary>
    [Fact]
    public async Task DeleteRegularDefinitionAsync_Should_Be_NoOp_When_Already_Inactive()
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

        // Second call — must not throw
        await service.DeleteRegularDefinitionAsync(new DeleteRegularIncomeDefinitionRequest { Id = created.Id }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var definition = await verifyContext.RegularIncomeDefinitions.FirstAsync(x => x.Id == created.Id);
        Assert.False(definition.IsActive);
    }

    /// <summary>
    /// DeleteRegularDefinitionAsync throws NotFoundException when a definition with the given Id does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteRegularDefinitionAsync_Should_Throw_When_Definition_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteRegularDefinitionAsync(
                new DeleteRegularIncomeDefinitionRequest { Id = 9999 },
                CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // DeleteRegularDefinitionPermanentlyAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// DeleteRegularDefinitionPermanentlyAsync permanently removes the definition from the database (hard delete).
    /// </summary>
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

    /// <summary>
    /// DeleteRegularDefinitionPermanentlyAsync throws NotFoundException when the definition does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteRegularDefinitionPermanentlyAsync_Should_Throw_When_Definition_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteRegularDefinitionPermanentlyAsync(
                new DeleteRegularIncomeDefinitionRequest { Id = 9999 },
                CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // SyncRegularIncomesForMonthAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// SyncRegularIncomesForMonthAsync creates incomes for all active definitions
    /// and is idempotent — repeated calls do not produce duplicate entries.
    /// </summary>
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

    /// <summary>
    /// SyncRegularIncomesForMonthAsync does not create incomes when the month is closed.
    /// The call is silent — it does not throw, it simply does nothing.
    /// </summary>
    [Fact]
    public async Task SyncRegularIncomesForMonthAsync_Should_Skip_Closed_Month()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 3, IsClosed = true });
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

        await service.SyncRegularIncomesForMonthAsync(2026, 3, CancellationToken.None);

        var incomes = await service.GetMonthIncomesAsync(2026, 3, CancellationToken.None);
        Assert.Empty(incomes);
    }

    /// <summary>
    /// SyncRegularIncomesForMonthAsync does not create incomes for definitions with IsActive=false.
    /// Inactive definitions are skipped during synchronization.
    /// </summary>
    [Fact]
    public async Task SyncRegularIncomesForMonthAsync_Should_Not_Sync_Inactive_Definitions()
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
        var definition = await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);

        await service.DeleteRegularDefinitionAsync(
            new DeleteRegularIncomeDefinitionRequest { Id = definition.Id },
            CancellationToken.None);

        await service.SyncRegularIncomesForMonthAsync(2026, 4, CancellationToken.None);

        var incomes = await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None);
        Assert.Empty(incomes);
    }

    /// <summary>
    /// SyncRegularIncomesForMonthAsync clamps DayOfMonth to the last day of a short month.
    /// A definition with DayOfMonth=31 in February 2026 (28 days) creates an income on Feb 28.
    /// Guards against creating invalid dates (e.g. Feb 31).
    /// </summary>
    [Fact]
    public async Task SyncRegularIncomesForMonthAsync_Should_Clamp_DayOfMonth_To_Last_Day_Of_Short_Month()
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
            Name = "Wyplata koniec miesiaca",
            Amount = 3000m,
            DayOfMonth = 31,
            AccountId = accountId
        }, CancellationToken.None);

        // February 2026 has 28 days
        await service.SyncRegularIncomesForMonthAsync(2026, 2, CancellationToken.None);

        var incomes = await service.GetMonthIncomesAsync(2026, 2, CancellationToken.None);

        var income = Assert.Single(incomes);
        Assert.Equal(new DateOnly(2026, 2, 28), income.ExpectedDayOfMonth);
    }

    /// <summary>
    /// Deleting a regular income occurrence in the current month does not block income creation
    /// in subsequent months — the definition stays active and sync works for other months.
    /// </summary>
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

    // -------------------------------------------------------------------------
    // AddRegularDefinitionToMonthAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// AddRegularDefinitionToMonthAsync adds an active definition as an income in a specific month
    /// and returns true. The resulting income has IsRegular=true and a correctly mapped RegularIncomeDefinitionId.
    /// </summary>
    [Fact]
    public async Task AddRegularDefinitionToMonthAsync_Should_Add_Income_For_Month_And_Return_True()
    {
        int accountId;
        int definitionId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        var definition = await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);
        definitionId = definition.Id;

        var result = await service.AddRegularDefinitionToMonthAsync(definitionId, 2026, 4, CancellationToken.None);

        Assert.True(result);

        var incomes = await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None);
        var income = Assert.Single(incomes);
        Assert.True(income.IsRegular);
        Assert.Equal("Wyplata", income.Name);
    }

    /// <summary>
    /// AddRegularDefinitionToMonthAsync returns false when an income from this definition
    /// already exists in the given month (idempotent — no duplicates).
    /// </summary>
    [Fact]
    public async Task AddRegularDefinitionToMonthAsync_Should_Return_False_When_Already_Added()
    {
        int accountId;
        int definitionId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        var definition = await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);
        definitionId = definition.Id;

        await service.AddRegularDefinitionToMonthAsync(definitionId, 2026, 4, CancellationToken.None);
        var secondResult = await service.AddRegularDefinitionToMonthAsync(definitionId, 2026, 4, CancellationToken.None);

        Assert.False(secondResult);

        var incomes = await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None);
        Assert.Single(incomes);
    }

    /// <summary>
    /// AddRegularDefinitionToMonthAsync returns false when the definition is inactive (IsActive=false).
    /// An inactive definition must not be added to any month.
    /// </summary>
    [Fact]
    public async Task AddRegularDefinitionToMonthAsync_Should_Return_False_When_Definition_Is_Inactive()
    {
        int accountId;
        int definitionId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        var definition = await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);
        definitionId = definition.Id;

        await service.DeleteRegularDefinitionAsync(
            new DeleteRegularIncomeDefinitionRequest { Id = definitionId },
            CancellationToken.None);

        var result = await service.AddRegularDefinitionToMonthAsync(definitionId, 2026, 4, CancellationToken.None);

        Assert.False(result);
        Assert.Empty(await service.GetMonthIncomesAsync(2026, 4, CancellationToken.None));
    }

    /// <summary>
    /// AddRegularDefinitionToMonthAsync throws BadRequestException when the month is closed.
    /// </summary>
    [Fact]
    public async Task AddRegularDefinitionToMonthAsync_Should_Throw_When_Month_Is_Closed()
    {
        int accountId;
        int definitionId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Rachunek", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 4, IsClosed = true });
            await context.SaveChangesAsync();
            accountId = account.Id;
        }

        var service = CreateService();
        var definition = await service.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);
        definitionId = definition.Id;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.AddRegularDefinitionToMonthAsync(definitionId, 2026, 4, CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // GetLiveBalanceAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// GetLiveBalanceAsync computes: AccountsBaseTotal (previous month closing balance, excluding savings accounts)
    /// + IncomesTotal (only incomes with ExpectedDayOfMonth &lt;= today) - ExpensesTotal - SavingsTransfersTotal.
    /// Savings accounts are excluded from AccountsBaseTotal.
    /// </summary>
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
        Assert.True(liveBalance.HasCompleteBalanceBase);
        Assert.Empty(liveBalance.MissingBalanceAccountNames);
    }

    /// <summary>
    /// GetLiveBalanceAsync includes only incomes with ExpectedDayOfMonth &lt;= today.
    /// Incomes with a future date in the current month are excluded from the balance calculation.
    /// </summary>
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

    /// <summary>
    /// GetLiveBalanceAsync subtracts the savings transfer amount when TransferDate &lt;= today.
    /// Future transfers (date &gt; today) are excluded.
    /// </summary>
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

    /// <summary>
    /// GetLiveBalanceAsync ignores a savings transfer when its TransferDate is in the future.
    /// A transfer scheduled for tomorrow must not reduce today's balance.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Ignore_Future_Savings_Transfer()
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

            context.MonthSavingsTransferItems.Add(new MonthSavingsTransferItem
            {
                MonthPlanId = monthPlan.Id,
                Amount = 500m,
                TransferDate = new DateOnly(2026, 4, 20) // after "today" (10 April)
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.Equal(0m, liveBalance.SavingsTransfersTotal);
        Assert.Equal(1000m, liveBalance.CurrentBalance);
    }

    /// <summary>
    /// GetLiveBalanceAsync excludes savings accounts (AccountType.Savings)
    /// from AccountsBaseTotal — only bank accounts contribute to the base balance.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Exclude_Savings_Accounts_From_Base_Total()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var bankAccount = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            var savingsAccount = new Account { Name = "Poduszka", Type = (int)AccountType.Savings };
            context.Accounts.AddRange(bankAccount, savingsAccount);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance { AccountId = bankAccount.Id, Year = 2026, Month = 3, ClosingBalance = 3000m },
                new AccountMonthBalance { AccountId = savingsAccount.Id, Year = 2026, Month = 3, ClosingBalance = 10000m });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        // Only the bank account balance (3000); the savings account (10000) is excluded
        Assert.Equal(3000m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync uses the immediately preceding month's balance
    /// when an account also has older historical balances.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Use_Immediately_Preceding_Month_Balance()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance { AccountId = account.Id, Year = 2026, Month = 1, ClosingBalance = 1000m },
                new AccountMonthBalance { AccountId = account.Id, Year = 2026, Month = 2, ClosingBalance = 2500m },
                // March 2026 is the previous month when querying April:
                new AccountMonthBalance { AccountId = account.Id, Year = 2026, Month = 3, ClosingBalance = 4000m });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        // The required immediately preceding balance for April is March = 4000.
        Assert.Equal(4000m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync marks its result incomplete when a non-savings account has no
    /// closing balance for the immediately preceding month. An older balance is not a valid substitute.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Report_Incomplete_When_Previous_Month_Balance_Is_Missing()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Konto bez marca", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.Add(new AccountMonthBalance
            {
                AccountId = account.Id,
                Year = 2026,
                Month = 2,
                ClosingBalance = 1500m
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.False(liveBalance.HasCompleteBalanceBase);
        Assert.Equal(new[] { "Konto bez marca" }, liveBalance.MissingBalanceAccountNames);
        Assert.Equal(0m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync treats a stored zero closing balance as complete input.
    /// A zero amount is different from a missing balance record.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Accept_Stored_Zero_Previous_Month_Balance()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Konto zerowe", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.Add(new AccountMonthBalance
            {
                AccountId = account.Id,
                Year = 2026,
                Month = 3,
                ClosingBalance = 0m
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.True(liveBalance.HasCompleteBalanceBase);
        Assert.Empty(liveBalance.MissingBalanceAccountNames);
        Assert.Equal(0m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync does not require a previous-month closing balance for a non-savings
    /// account that became active during the selected month.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Not_Require_Previous_Month_Balance_For_Account_Activated_In_Selected_Month()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Accounts.Add(new Account
            {
                Name = "Przywrocone w kwietniu",
                Type = (int)AccountType.Bank,
                ActiveFromUtc = DefaultNowUtc
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.True(liveBalance.HasCompleteBalanceBase);
        Assert.Empty(liveBalance.MissingBalanceAccountNames);
        Assert.Equal(0m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync includes the previous-month closing balance for an account
    /// activated during the selected month when that balance was explicitly recorded.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Include_Previous_Month_Balance_For_Account_Activated_In_Selected_Month_When_Recorded()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var baseAccount = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            var restoredAccount = new Account
            {
                Name = "Millenium",
                Type = (int)AccountType.Bank,
                ActiveFromUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            context.Accounts.AddRange(baseAccount, restoredAccount);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance
                {
                    AccountId = baseAccount.Id,
                    Year = 2026,
                    Month = 3,
                    ClosingBalance = 205.81m
                },
                new AccountMonthBalance
                {
                    AccountId = restoredAccount.Id,
                    Year = 2026,
                    Month = 3,
                    ClosingBalance = 104.88m
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.True(liveBalance.HasCompleteBalanceBase);
        Assert.Empty(liveBalance.MissingBalanceAccountNames);
        Assert.Equal(310.69m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync sums recorded previous-month non-savings balances even when
    /// an account was archived at the beginning of the selected month.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Include_Previous_Month_Balance_For_Account_Archived_In_Selected_Month_When_Recorded()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var baseAccount = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            var archivedAccount = new Account
            {
                Name = "Zamkniete konto",
                Type = (int)AccountType.Bank,
                IsArchived = true,
                ArchivedAtUtc = DefaultNowUtc
            };
            context.Accounts.AddRange(baseAccount, archivedAccount);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance
                {
                    AccountId = baseAccount.Id,
                    Year = 2026,
                    Month = 3,
                    ClosingBalance = 205.81m
                },
                new AccountMonthBalance
                {
                    AccountId = archivedAccount.Id,
                    Year = 2026,
                    Month = 3,
                    ClosingBalance = 104.88m
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.True(liveBalance.HasCompleteBalanceBase);
        Assert.Empty(liveBalance.MissingBalanceAccountNames);
        Assert.Equal(310.69m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync computes a closed historical month from the latest stored prior
    /// balances and does not retroactively require missing immediately preceding-month rows.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Use_Available_Historical_Balances_For_Closed_Month()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var accountWithOlderBalance = new Account { Name = "Historia", Type = (int)AccountType.Bank };
            var accountWithoutBalance = new Account { Name = "Bez historii", Type = (int)AccountType.Bank };
            context.Accounts.AddRange(accountWithOlderBalance, accountWithoutBalance);
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 4, IsClosed = true });
            await context.SaveChangesAsync();

            context.AccountMonthBalances.Add(new AccountMonthBalance
            {
                AccountId = accountWithOlderBalance.Id,
                Year = 2026,
                Month = 2,
                ClosingBalance = 1500m
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.True(liveBalance.HasCompleteBalanceBase);
        Assert.Empty(liveBalance.MissingBalanceAccountNames);
        Assert.Equal(1500m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync ignores an account archived before the selected month,
    /// including when the selected month is already closed.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Ignore_Account_Archived_Before_Selected_Closed_Month()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var activeAccount = new Account { Name = "Aktywne", Type = (int)AccountType.Bank };
            var archivedAccount = new Account
            {
                Name = "Archiwalne",
                Type = (int)AccountType.Bank,
                IsArchived = true,
                ArchivedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)
            };
            context.Accounts.AddRange(activeAccount, archivedAccount);
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 4, IsClosed = true });
            await context.SaveChangesAsync();

            context.AccountMonthBalances.Add(new AccountMonthBalance
            {
                AccountId = activeAccount.Id,
                Year = 2026,
                Month = 3,
                ClosingBalance = 1200m
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.True(liveBalance.HasCompleteBalanceBase);
        Assert.Empty(liveBalance.MissingBalanceAccountNames);
        Assert.Equal(1200m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync ignores an account archived during the selected month
    /// because it is no longer expected to have that month's closing balance.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Ignore_Account_Archived_During_Selected_Month()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var activeAccount = new Account { Name = "Aktywne", Type = (int)AccountType.Bank };
            var archivedAccount = new Account
            {
                Name = "Archiwalne",
                Type = (int)AccountType.Bank,
                IsArchived = true,
                ArchivedAtUtc = DefaultNowUtc.AddDays(-1)
            };
            context.Accounts.AddRange(activeAccount, archivedAccount);
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 4, IsClosed = true });
            await context.SaveChangesAsync();

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance { AccountId = activeAccount.Id, Year = 2026, Month = 3, ClosingBalance = 1200m },
                new AccountMonthBalance { AccountId = archivedAccount.Id, Year = 2026, Month = 3, ClosingBalance = 300m });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.True(liveBalance.HasCompleteBalanceBase);
        Assert.Equal(1200m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync retains an archived account for a historical month
    /// when it remained active until after that month ended.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Include_Account_Archived_After_Selected_Month_Ended()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var activeAccount = new Account { Name = "Aktywne", Type = (int)AccountType.Bank };
            var archivedAccount = new Account
            {
                Name = "Archiwalne pozniej",
                Type = (int)AccountType.Bank,
                IsArchived = true,
                ArchivedAtUtc = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc)
            };
            context.Accounts.AddRange(activeAccount, archivedAccount);
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 4, IsClosed = true });
            await context.SaveChangesAsync();

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance { AccountId = activeAccount.Id, Year = 2026, Month = 3, ClosingBalance = 1200m },
                new AccountMonthBalance { AccountId = archivedAccount.Id, Year = 2026, Month = 3, ClosingBalance = 300m });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.True(liveBalance.HasCompleteBalanceBase);
        Assert.Equal(1500m, liveBalance.AccountsBaseTotal);
    }

    /// <summary>
    /// GetLiveBalanceAsync subtracts actual unplanned spending from live balance.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Subtract_Unplanned_Actual_Expense()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            var category = new Category { Name = "Inne", Color = "#123456" };
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

            context.Expenses.Add(new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Nagly koszt",
                CategoryId = category.Id,
                PlannedAmount = 0m,
                ActualAmount = 75m
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.Equal(75m, liveBalance.ExpensesTotal);
        Assert.Equal(925m, liveBalance.CurrentBalance);
    }

    /// <summary>
    /// GetLiveBalanceAsync returns zero balance when no accounts or historical balances exist.
    /// All components are zero, CurrentBalance = 0.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Return_Zero_When_No_Accounts_Exist()
    {
        var service = CreateService();

        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.Equal(0m, liveBalance.AccountsBaseTotal);
        Assert.Equal(0m, liveBalance.IncomesTotal);
        Assert.Equal(0m, liveBalance.ExpensesTotal);
        Assert.Equal(0m, liveBalance.SavingsTransfersTotal);
        Assert.Equal(0m, liveBalance.CurrentBalance);
    }

    /// <summary>
    /// GetLiveBalanceAsync returns ExpensesTotal=0 and SavingsTransfersTotal=0 when no MonthPlan exists
    /// for the given month — a plan is not required to compute the balance.
    /// </summary>
    [Fact]
    public async Task GetLiveBalanceAsync_Should_Return_Zero_Expenses_When_No_Month_Plan()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.Add(new AccountMonthBalance
            {
                AccountId = account.Id,
                Year = 2026,
                Month = 3,
                ClosingBalance = 1500m
            });

            context.Incomes.Add(new Income
            {
                Year = 2026,
                Month = 4,
                Name = "Wpływ",
                Amount = 300m,
                ExpectedDayOfMonth = new DateOnly(2026, 4, 5),
                AccountId = account.Id
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var liveBalance = await service.GetLiveBalanceAsync(2026, 4, CancellationToken.None);

        Assert.Equal(0m, liveBalance.ExpensesTotal);
        Assert.Equal(0m, liveBalance.SavingsTransfersTotal);
        Assert.Equal(1800m, liveBalance.CurrentBalance); // 1500 base + 300 income
    }
}

