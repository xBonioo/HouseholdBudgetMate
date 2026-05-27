using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class UserScopingTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DbContextOptions<ApplicationDbContext> NewOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static CurrentUserContext CreateCurrentUserContext(string userId, string? budgetOwnerUserId = null) =>
        new() { UserId = userId, BudgetOwnerUserId = budgetOwnerUserId ?? userId };

    private static Loan CreateLoan(string name) => new()
    {
        Name = name,
        LoanType = 1,
        InterestMode = 1,
        Principal = 1000,
        InterestRate = 5,
        RepaymentDayOfMonth = 10,
        StartDate = new DateOnly(2026, 1, 1),
        EndDate = new DateOnly(2026, 12, 1),
        IsActive = true
    };

    // ── User stamping & query filter ─────────────────────────────────────────

    /// <summary>
    /// Verifies that SaveChanges stamps new entities with the current user's ID
    /// and that query filters isolate data so each user only sees their own records.
    /// </summary>
    [Fact]
    public async Task SaveChanges_Should_Stamp_New_Entities_With_Current_User_And_Filter_By_User()
    {
        var options = NewOptions();

        await using (var setupContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            setupContext.Users.AddRange(
                new User { Id = "user-a", Username = "user-a", PasswordHash = "11111111" },
                new User { Id = "user-b", Username = "user-b", PasswordHash = "22222222" });
            await setupContext.SaveChangesAsync();
        }

        await using (var userAContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            userAContext.Accounts.Add(new Account { Name = "A bank", Type = (int)AccountType.Bank, Order = 1 });
            userAContext.Loans.Add(CreateLoan("A loan"));
            await userAContext.SaveChangesAsync();
        }

        await using (var userBContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-b")))
        {
            var visibleAccounts = await userBContext.Accounts.ToListAsync();
            visibleAccounts.Should().BeEmpty();

            userBContext.Accounts.Add(new Account { Name = "B bank", Type = (int)AccountType.Bank, Order = 1 });
            userBContext.Loans.Add(CreateLoan("B loan"));
            await userBContext.SaveChangesAsync();
        }

        await using (var verificationContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            var userAAccounts = await verificationContext.Accounts.ToListAsync();
            userAAccounts.Should().ContainSingle();
            userAAccounts[0].UserId.Should().Be("user-a");

            var allAccounts = await verificationContext.Accounts
                .IgnoreQueryFilters()
                .OrderBy(x => x.UserId)
                .ToListAsync();
            allAccounts.Select(x => x.UserId).Should().Equal("user-a", "user-b");

            var userALoans = await verificationContext.Loans.ToListAsync();
            userALoans.Should().ContainSingle();
            userALoans[0].UserId.Should().Be("user-a");

            var allLoans = await verificationContext.Loans
                .IgnoreQueryFilters()
                .OrderBy(x => x.UserId)
                .ToListAsync();
            allLoans.Select(x => x.UserId).Should().Equal("user-a", "user-b");
        }
    }

    /// <summary>
    /// Verifies that a SharedBudget user reads and writes all data under the budget owner's UserId,
    /// so the owner sees everything written by the spouse as their own records.
    /// </summary>
    [Fact]
    public async Task Shared_Budget_User_Should_Read_And_Write_Data_For_Budget_Owner()
    {
        var options = NewOptions();

        await using (var setupContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            setupContext.Users.AddRange(
                new User { Id = "user-a", Username = "user-a", PasswordHash = "hash-a" },
                new User
                {
                    Id = "user-b",
                    Username = "user-b",
                    PasswordHash = "hash-b",
                    HouseholdMode = (int)HouseholdMode.SharedBudget,
                    BudgetOwnerUserId = "user-a"
                });

            setupContext.Accounts.Add(new Account { Name = "Shared bank", Type = (int)AccountType.Bank, Order = 1 });
            setupContext.Loans.Add(CreateLoan("Shared loan"));

            await setupContext.SaveChangesAsync();
        }

        await using (var spouseContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-b", "user-a")))
        {
            var visibleAccounts = await spouseContext.Accounts.ToListAsync();
            visibleAccounts.Should().ContainSingle(x => x.Name == "Shared bank");

            var visibleLoans = await spouseContext.Loans.ToListAsync();
            visibleLoans.Should().ContainSingle(x => x.Name == "Shared loan");

            spouseContext.Accounts.Add(new Account { Name = "Added by spouse", Type = (int)AccountType.Bank, Order = 2 });
            spouseContext.Loans.Add(CreateLoan("Loan added by spouse"));

            await spouseContext.SaveChangesAsync();
        }

        await using (var verificationContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            var accounts = await verificationContext.Accounts.OrderBy(x => x.Order).ToListAsync();
            accounts.Should().HaveCount(2);
            accounts.Select(x => x.UserId).Should().OnlyContain(x => x == "user-a");

            var loans = await verificationContext.Loans.OrderBy(x => x.Name).ToListAsync();
            loans.Should().HaveCount(2);
            loans.Select(x => x.UserId).Should().OnlyContain(x => x == "user-a");
        }
    }

    // ── Timestamps ───────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that SaveChanges sets both CreatedAtUtc and UpdatedAtUtc on newly added ITimestampable entities.
    /// </summary>
    [Fact]
    public async Task SaveChanges_Should_Set_Timestamps_On_New_Entity()
    {
        var options = NewOptions();
        var before = DateTime.UtcNow.AddSeconds(-1);

        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            ctx.Users.Add(new User { Id = "user-a", Username = "user-a", PasswordHash = "hash" });
            await ctx.SaveChangesAsync();
        }

        int accountId;
        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            var account = new Account { Name = "Test", Type = (int)AccountType.Cash, Order = 1 };
            ctx.Accounts.Add(account);
            await ctx.SaveChangesAsync();
            accountId = account.Id;
        }

        var after = DateTime.UtcNow.AddSeconds(1);

        await using var verifyCtx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a"));
        var saved = await verifyCtx.Accounts.SingleAsync(x => x.Id == accountId);

        saved.CreatedAtUtc.Should().BeAfter(before).And.BeBefore(after);
        saved.UpdatedAtUtc.Should().BeAfter(before).And.BeBefore(after);
    }

    /// <summary>
    /// Verifies that SaveChanges preserves CreatedAtUtc and only updates UpdatedAtUtc when modifying an entity.
    /// </summary>
    [Fact]
    public async Task SaveChanges_Should_Preserve_CreatedAtUtc_And_Update_UpdatedAtUtc_On_Modify()
    {
        var options = NewOptions();

        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            ctx.Users.Add(new User { Id = "user-a", Username = "user-a", PasswordHash = "hash" });
            await ctx.SaveChangesAsync();
        }

        int accountId;
        DateTime originalCreatedAt;

        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            var account = new Account { Name = "Original", Type = (int)AccountType.Cash, Order = 1 };
            ctx.Accounts.Add(account);
            await ctx.SaveChangesAsync();
            accountId = account.Id;
            originalCreatedAt = account.CreatedAtUtc;
        }

        // Small delay so UpdatedAtUtc will differ from CreatedAtUtc
        await Task.Delay(10);

        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            var account = await ctx.Accounts.SingleAsync(x => x.Id == accountId);
            account.Name = "Modified";
            await ctx.SaveChangesAsync();
        }

        await using var verifyCtx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a"));
        var saved = await verifyCtx.Accounts.SingleAsync(x => x.Id == accountId);

        saved.CreatedAtUtc.Should().Be(originalCreatedAt);
        saved.UpdatedAtUtc.Should().BeOnOrAfter(originalCreatedAt);
    }

    // ── UserId stamping ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that modifying an existing entity does not overwrite its UserId.
    /// Only newly Added entities get their UserId stamped.
    /// </summary>
    [Fact]
    public async Task SaveChanges_Should_Not_Restamp_UserId_On_Modified_Entity()
    {
        var options = NewOptions();

        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            ctx.Users.AddRange(
                new User { Id = "user-a", Username = "user-a", PasswordHash = "hash-a" },
                new User { Id = "user-b", Username = "user-b", PasswordHash = "hash-b" });
            await ctx.SaveChangesAsync();
        }

        int accountId;
        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            var account = new Account { Name = "Original", Type = (int)AccountType.Cash, Order = 1 };
            ctx.Accounts.Add(account);
            await ctx.SaveChangesAsync();
            accountId = account.Id;
        }

        // Modify the account while acting as user-b (shared budget with user-a as owner)
        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-b", "user-a")))
        {
            var account = await ctx.Accounts.SingleAsync(x => x.Id == accountId);
            account.Name = "Modified by spouse";
            await ctx.SaveChangesAsync();
        }

        await using var verifyCtx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a"));
        var saved = await verifyCtx.Accounts.SingleAsync(x => x.Id == accountId);

        // UserId must still be user-a, not user-b
        saved.UserId.Should().Be("user-a");
        saved.Name.Should().Be("Modified by spouse");
    }

    /// <summary>
    /// Verifies that an interactive identity without an established budget owner cannot
    /// read or write budget data.
    /// </summary>
    [Fact]
    public async Task Context_Null_BudgetOwnerUserId_Fails_Closed()
    {
        var options = NewOptions();

        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            ctx.Users.Add(new User { Id = "user-a", Username = "user-a", PasswordHash = "hash" });
            await ctx.SaveChangesAsync();
        }

        var contextWithNullOwner = new CurrentUserContext { UserId = "user-a", BudgetOwnerUserId = null };

        await using (var seedContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            seedContext.Accounts.Add(new Account { Name = "Protected Account", Type = (int)AccountType.Bank, Order = 1 });
            await seedContext.SaveChangesAsync();
        }

        await using var unauthorizedContext = new ApplicationDbContext(options, contextWithNullOwner);
        (await unauthorizedContext.Accounts.ToListAsync()).Should().BeEmpty();

        unauthorizedContext.Accounts.Add(new Account { Name = "Rejected Account", Type = (int)AccountType.Bank, Order = 2 });
        await unauthorizedContext.Invoking(x => x.SaveChangesAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*authenticated user or explicit system scope*");
    }

    // ── Other entity types ────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that MonthPlan records are scoped per user and are not visible to other users.
    /// </summary>
    [Fact]
    public async Task MonthPlan_Should_Be_Scoped_To_User()
    {
        var options = NewOptions();

        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            ctx.Users.AddRange(
                new User { Id = "user-a", Username = "user-a", PasswordHash = "hash-a" },
                new User { Id = "user-b", Username = "user-b", PasswordHash = "hash-b" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctxA = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            ctxA.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 1 });
            ctxA.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 2 });
            await ctxA.SaveChangesAsync();
        }

        await using (var ctxB = new ApplicationDbContext(options, CreateCurrentUserContext("user-b")))
        {
            var plans = await ctxB.MonthPlans.ToListAsync();
            plans.Should().BeEmpty();

            ctxB.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 1 });
            await ctxB.SaveChangesAsync();
        }

        await using var verifyCtxA = new ApplicationDbContext(options, CreateCurrentUserContext("user-a"));
        var userAPlans = await verifyCtxA.MonthPlans.ToListAsync();
        userAPlans.Should().HaveCount(2);
        userAPlans.Should().OnlyContain(x => x.UserId == "user-a");
    }

    /// <summary>
    /// Verifies that Income records are scoped per user and are not visible to other users.
    /// </summary>
    [Fact]
    public async Task Income_Should_Be_Scoped_To_User()
    {
        var options = NewOptions();

        await using (var ctx = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            ctx.Users.AddRange(
                new User { Id = "user-a", Username = "user-a", PasswordHash = "hash-a" },
                new User { Id = "user-b", Username = "user-b", PasswordHash = "hash-b" });
            await ctx.SaveChangesAsync();
        }

        int accountIdA;
        int accountIdB;

        await using (var ctxA = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            var acc = new Account { Name = "A Bank", Type = (int)AccountType.Bank, Order = 1 };
            ctxA.Accounts.Add(acc);
            await ctxA.SaveChangesAsync();
            accountIdA = acc.Id;

            ctxA.Incomes.Add(new Income
            {
                Name = "Salary A",
                Amount = 5000m,
                Year = 2026,
                Month = 5,
                ExpectedDayOfMonth = new DateOnly(2026, 5, 10),
                AccountId = accountIdA
            });
            await ctxA.SaveChangesAsync();
        }

        await using (var ctxB = new ApplicationDbContext(options, CreateCurrentUserContext("user-b")))
        {
            var incomes = await ctxB.Incomes.ToListAsync();
            incomes.Should().BeEmpty();

            var acc = new Account { Name = "B Bank", Type = (int)AccountType.Bank, Order = 1 };
            ctxB.Accounts.Add(acc);
            await ctxB.SaveChangesAsync();
            accountIdB = acc.Id;

            ctxB.Incomes.Add(new Income
            {
                Name = "Salary B",
                Amount = 4000m,
                Year = 2026,
                Month = 5,
                ExpectedDayOfMonth = new DateOnly(2026, 5, 15),
                AccountId = accountIdB
            });
            await ctxB.SaveChangesAsync();
        }

        await using var verifyCtxA = new ApplicationDbContext(options, CreateCurrentUserContext("user-a"));
        var userAIncomes = await verifyCtxA.Incomes.ToListAsync();
        userAIncomes.Should().ContainSingle(x => x.Name == "Salary A");
        userAIncomes[0].UserId.Should().Be("user-a");
    }

    /// <summary>
    /// Verifies that absence of an interactive or explicit system context does not expose
    /// or permit changes to the technical owner's budget.
    /// </summary>
    [Fact]
    public async Task No_UserContext_Fails_Closed_For_Technical_Owner_Budget()
    {
        var options = NewOptions();

        await using (var ctx = new ApplicationDbContext(options, CurrentUserContext.ForTechnicalOwner()))
        {
            ctx.Users.Add(new User
            {
                Id = User.DefaultUserId,
                Username = "default",
                PasswordHash = "hash"
            });
            ctx.Categories.Add(new Category { Name = "Protected Category", Color = "#123456" });
            ctx.Accounts.Add(new Account { Name = "Protected Account", Type = (int)AccountType.Cash, Order = 1 });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ApplicationDbContext(options))
        {
            (await ctx.Accounts.ToListAsync()).Should().BeEmpty();
            (await ctx.Categories.ToListAsync()).Should().BeEmpty();

            ctx.Accounts.Add(new Account { Name = "Rejected Account", Type = (int)AccountType.Cash, Order = 2 });
            await ctx.Invoking(x => x.SaveChangesAsync())
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*authenticated user or explicit system scope*");
        }

        await using var verifyCtx = new ApplicationDbContext(options, CurrentUserContext.ForTechnicalOwner());
        (await verifyCtx.Accounts.ToListAsync()).Should().ContainSingle(x => x.Name == "Protected Account");
        (await verifyCtx.Categories.ToListAsync()).Should().ContainSingle(x => x.Name == "Protected Category");
    }
}
