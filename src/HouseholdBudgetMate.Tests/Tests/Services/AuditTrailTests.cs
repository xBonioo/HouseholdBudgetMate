using System.Text.Json;
using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Contracts.Audit.Requests;
using HouseholdBudgetMate.Application.Auditing;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class AuditTrailTests
{
    [Fact]
    public async Task SaveChanges_WhenEntityIsCreated_Should_LogRealGeneratedEntityId()
    {
        var currentUser = CreateCurrentUserContext("actor-user", "owner-user");
        var options = NewOptions(currentUser);
        int accountId;

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            SeedUsers(setupContext);
            setupContext.Accounts.Add(new Account { Name = "Konto", Type = 1, Order = 1 });
            await setupContext.SaveChangesAsync();
            accountId = await setupContext.Accounts.Select(x => x.Id).SingleAsync();
        }

        await using var verifyContext = new ApplicationDbContext(options, currentUser);
        var auditLog = await verifyContext.AuditLogs
            .Where(x => x.EntityType == nameof(Account) && x.Operation == "Create")
            .SingleAsync();

        auditLog.EntityId.Should().Be(accountId);
        auditLog.EntityId.Should().NotBe(int.MaxValue);
    }

    [Fact]
    public async Task SaveChanges_WhenExpenseAmountChanges_Should_LogOldAndNewAmount()
    {
        var currentUser = CreateCurrentUserContext("actor-user", "owner-user");
        var options = NewOptions(currentUser);
        int expenseId;

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            SeedUsers(setupContext);
            setupContext.Categories.Add(new Category { Name = "Jedzenie", Color = "#123456" });
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 5 });
            await setupContext.SaveChangesAsync();

            var category = await setupContext.Categories.SingleAsync();
            var monthPlan = await setupContext.MonthPlans.SingleAsync();
            setupContext.Expenses.Add(new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy",
                CategoryId = category.Id,
                PlannedAmount = 100,
                ActualAmount = 80,
                Order = 1
            });
            await setupContext.SaveChangesAsync();
            expenseId = await setupContext.Expenses.Select(x => x.Id).SingleAsync();
        }

        await using (var updateContext = new ApplicationDbContext(options, currentUser))
        {
            var expense = await updateContext.Expenses.SingleAsync(x => x.Id == expenseId);
            expense.ActualAmount = 95;
            expense.PlannedAmount = 110;
            await updateContext.SaveChangesAsync();
        }

        await using var verifyContext = new ApplicationDbContext(options, currentUser);
        var auditLog = await verifyContext.AuditLogs
            .Where(x => x.EntityType == nameof(Expense)
                        && x.EntityId == expenseId
                        && x.Operation == "Update")
            .OrderByDescending(x => x.Id)
            .FirstAsync();

        auditLog.UserId.Should().Be("actor-user");
        auditLog.BudgetOwnerUserId.Should().Be("owner-user");

        var oldValues = DeserializeValues(auditLog.OldValuesJson);
        var newValues = DeserializeValues(auditLog.NewValuesJson);
        oldValues["ActualAmount"].GetDecimal().Should().Be(80);
        newValues["ActualAmount"].GetDecimal().Should().Be(95);
        oldValues["PlannedAmount"].GetDecimal().Should().Be(100);
        newValues["PlannedAmount"].GetDecimal().Should().Be(110);
    }

    [Fact]
    public async Task SaveChanges_WhenExpenseIsSoftDeleted_Should_LogDeleteOperation()
    {
        var currentUser = CreateCurrentUserContext("actor-user", "owner-user");
        var options = NewOptions(currentUser);
        int expenseId;

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            SeedUsers(setupContext);
            setupContext.Categories.Add(new Category { Name = "Rachunki", Color = "#abcdef" });
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 5 });
            await setupContext.SaveChangesAsync();

            setupContext.Expenses.Add(new Expense
            {
                MonthPlanId = await setupContext.MonthPlans.Select(x => x.Id).SingleAsync(),
                Name = "Prąd",
                CategoryId = await setupContext.Categories.Select(x => x.Id).SingleAsync(),
                PlannedAmount = 250,
                ActualAmount = 250,
                Order = 1
            });
            await setupContext.SaveChangesAsync();
            expenseId = await setupContext.Expenses.Select(x => x.Id).SingleAsync();
        }

        await using (var updateContext = new ApplicationDbContext(options, currentUser))
        {
            var expense = await updateContext.Expenses.SingleAsync(x => x.Id == expenseId);
            expense.IsDeleted = true;
            expense.DeletedAtUtc = DateTime.UtcNow;
            await updateContext.SaveChangesAsync();
        }

        await using var verifyContext = new ApplicationDbContext(options, currentUser);
        var auditLog = await verifyContext.AuditLogs
            .Where(x => x.EntityType == nameof(Expense)
                        && x.EntityId == expenseId
                        && x.Operation == "Delete")
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        auditLog!.NewValuesJson.Should().Contain("IsDeleted");
    }

    [Fact]
    public async Task SaveChanges_WhenGeneratedLoanExpenseIsSoftDeleted_Should_LogUpdateOperation()
    {
        var currentUser = CreateCurrentUserContext("actor-user", "owner-user");
        var options = NewOptions(currentUser);
        int expenseId;

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            SeedUsers(setupContext);
            setupContext.Categories.Add(new Category { Name = "Kredyt", Color = "#abcdef" });
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 5 });
            setupContext.Loans.Add(new Loan
            {
                Name = "Kredyt",
                LoanType = 1,
                InterestMode = 1,
                Principal = 1000,
                InterestRate = 5,
                RepaymentDayOfMonth = 10,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 1),
                IsActive = true
            });
            await setupContext.SaveChangesAsync();

            setupContext.LoanInstallments.Add(new LoanInstallment
            {
                LoanId = await setupContext.Loans.Select(x => x.Id).SingleAsync(),
                Year = 2026,
                Month = 5,
                DueDate = new DateOnly(2026, 5, 10),
                Amount = 100,
                PrincipalAmount = 80,
                InterestAmount = 20
            });
            await setupContext.SaveChangesAsync();

            setupContext.Expenses.Add(new Expense
            {
                MonthPlanId = await setupContext.MonthPlans.Select(x => x.Id).SingleAsync(),
                Name = "Kredyt - rata",
                CategoryId = await setupContext.Categories.Select(x => x.Id).SingleAsync(),
                LoanInstallmentId = await setupContext.LoanInstallments.Select(x => x.Id).SingleAsync(),
                PlannedAmount = 100,
                ActualAmount = 0,
                Order = 1
            });
            await setupContext.SaveChangesAsync();
            expenseId = await setupContext.Expenses.Select(x => x.Id).SingleAsync();
        }

        await using (var updateContext = new ApplicationDbContext(options, currentUser))
        {
            var expense = await updateContext.Expenses.SingleAsync(x => x.Id == expenseId);
            expense.IsDeleted = true;
            expense.DeletedAtUtc = DateTime.UtcNow;
            await updateContext.SaveChangesAsync();
        }

        await using var verifyContext = new ApplicationDbContext(options, currentUser);
        var auditLog = await verifyContext.AuditLogs
            .Where(x => x.EntityType == nameof(Expense) && x.EntityId == expenseId)
            .OrderByDescending(x => x.Id)
            .FirstAsync();

        auditLog.Operation.Should().Be("Update");
        auditLog.NewValuesJson.Should().Contain("IsDeleted");
    }

    [Fact]
    public async Task SaveChanges_WhenLoanInstallmentIsMarkedPaid_Should_LogInstallmentUpdate()
    {
        var currentUser = CreateCurrentUserContext("actor-user", "owner-user");
        var options = NewOptions(currentUser);
        int installmentId;

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            SeedUsers(setupContext);
            setupContext.Loans.Add(new Loan
            {
                Name = "Kredyt",
                LoanType = 1,
                InterestMode = 1,
                Principal = 1000,
                InterestRate = 5,
                RepaymentDayOfMonth = 10,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 1),
                IsActive = true
            });
            await setupContext.SaveChangesAsync();

            setupContext.LoanInstallments.Add(new LoanInstallment
            {
                LoanId = await setupContext.Loans.Select(x => x.Id).SingleAsync(),
                Year = 2026,
                Month = 5,
                DueDate = new DateOnly(2026, 5, 10),
                Amount = 100,
                PrincipalAmount = 80,
                InterestAmount = 20
            });
            await setupContext.SaveChangesAsync();
            installmentId = await setupContext.LoanInstallments.Select(x => x.Id).SingleAsync();
        }

        await using (var updateContext = new ApplicationDbContext(options, currentUser))
        {
            var installment = await updateContext.LoanInstallments.SingleAsync(x => x.Id == installmentId);
            installment.IsPaid = true;
            installment.PaidAtUtc = DateTime.UtcNow;
            await updateContext.SaveChangesAsync();
        }

        await using var verifyContext = new ApplicationDbContext(options, currentUser);
        var auditLog = await verifyContext.AuditLogs
            .Where(x => x.EntityType == nameof(LoanInstallment)
                        && x.EntityId == installmentId
                        && x.Operation == "Update")
            .OrderByDescending(x => x.Id)
            .FirstAsync();

        auditLog.NewValuesJson.Should().Contain("IsPaid");
        auditLog.NewValuesJson.Should().Contain("PaidAtUtc");
    }


    [Fact]
    public async Task SaveChanges_WhenAccountMonthBalanceChanges_Should_LogBalanceChange()
    {
        var currentUser = CreateCurrentUserContext("actor-user", "owner-user");
        var options = NewOptions(currentUser);
        int balanceId;

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            SeedUsers(setupContext);
            setupContext.Accounts.Add(new Account { Name = "Konto główne", Type = 1, Order = 1 });
            await setupContext.SaveChangesAsync();

            setupContext.AccountMonthBalances.Add(new AccountMonthBalance
            {
                AccountId = await setupContext.Accounts.Select(x => x.Id).SingleAsync(),
                Year = 2026,
                Month = 5,
                ClosingBalance = 1000
            });
            await setupContext.SaveChangesAsync();
            balanceId = await setupContext.AccountMonthBalances.Select(x => x.Id).SingleAsync();
        }

        await using (var updateContext = new ApplicationDbContext(options, currentUser))
        {
            var balance = await updateContext.AccountMonthBalances.SingleAsync(x => x.Id == balanceId);
            balance.ClosingBalance = 1250;
            await updateContext.SaveChangesAsync();
        }

        await using var verifyContext = new ApplicationDbContext(options, currentUser);
        var auditLog = await verifyContext.AuditLogs
            .Where(x => x.EntityType == nameof(AccountMonthBalance)
                        && x.EntityId == balanceId
                        && x.Operation == "Update")
            .OrderByDescending(x => x.Id)
            .FirstAsync();

        var oldValues = DeserializeValues(auditLog.OldValuesJson);
        var newValues = DeserializeValues(auditLog.NewValuesJson);
        oldValues["ClosingBalance"].GetDecimal().Should().Be(1000);
        newValues["ClosingBalance"].GetDecimal().Should().Be(1250);
    }

    [Fact]
    public async Task SaveChanges_WhenExpenseLineItemIsCreated_Should_LogLineItemWithGeneratedId()
    {
        var currentUser = CreateCurrentUserContext("actor-user", "owner-user");
        var options = NewOptions(currentUser);
        int lineItemId;

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            SeedUsers(setupContext);
            setupContext.Categories.Add(new Category { Name = "Dom", Color = "#123456", SupportsLineItems = true });
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 5 });
            await setupContext.SaveChangesAsync();

            setupContext.Expenses.Add(new Expense
            {
                MonthPlanId = await setupContext.MonthPlans.Select(x => x.Id).SingleAsync(),
                Name = "Zakupy w Lidlu",
                CategoryId = await setupContext.Categories.Select(x => x.Id).SingleAsync(),
                PlannedAmount = 100,
                ActualAmount = 0,
                Order = 1
            });
            await setupContext.SaveChangesAsync();

            setupContext.ExpenseLineItems.Add(new ExpenseLineItem
            {
                ExpenseId = await setupContext.Expenses.Select(x => x.Id).SingleAsync(),
                Description = "Płatność za elewację",
                Amount = 75,
                OccurredAt = new DateOnly(2026, 5, 12)
            });
            await setupContext.SaveChangesAsync();
            lineItemId = await setupContext.ExpenseLineItems.Select(x => x.Id).SingleAsync();
        }

        await using var verifyContext = new ApplicationDbContext(options, currentUser);
        var auditLog = await verifyContext.AuditLogs
            .Where(x => x.EntityType == nameof(ExpenseLineItem) && x.Operation == "Create")
            .SingleAsync();

        auditLog.EntityId.Should().Be(lineItemId);
        var newValues = DeserializeValues(auditLog.NewValuesJson);
        newValues["Description"].GetString().Should().Be("Płatność za elewację");
    }

    [Fact]
    public async Task AuditService_Should_ShowFriendlyContextForExpenseAndLineItem()
    {
        var currentUser = CreateCurrentUserContext("admin-user", "owner-user");
        var options = NewOptions(currentUser);
        int expenseAuditId;
        int lineItemAuditId;

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            setupContext.Users.AddRange(
                new User
                {
                    Id = "admin-user",
                    Username = "Admin",
                    PasswordHash = "hash",
                    IsAdmin = true,
                    BudgetOwnerUserId = "owner-user"
                },
                new User
                {
                    Id = "owner-user",
                    Username = "Owner",
                    PasswordHash = "hash",
                    BudgetOwnerUserId = "owner-user"
                });
            setupContext.Categories.Add(new Category { Name = "Dom", Color = "#123456", SupportsLineItems = true });
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 5 });
            await setupContext.SaveChangesAsync();

            setupContext.Expenses.Add(new Expense
            {
                MonthPlanId = await setupContext.MonthPlans.Select(x => x.Id).SingleAsync(),
                Name = "Zakupy w Lidlu",
                CategoryId = await setupContext.Categories.Select(x => x.Id).SingleAsync(),
                PlannedAmount = 100,
                ActualAmount = 0,
                Order = 1
            });
            await setupContext.SaveChangesAsync();

            var expenseId = await setupContext.Expenses.Select(x => x.Id).SingleAsync();
            setupContext.ExpenseLineItems.Add(new ExpenseLineItem
            {
                ExpenseId = expenseId,
                Description = "Płatność za elewację",
                Amount = 75,
                OccurredAt = new DateOnly(2026, 5, 12)
            });
            await setupContext.SaveChangesAsync();

            expenseAuditId = await setupContext.AuditLogs
                .Where(x => x.EntityType == nameof(Expense))
                .Select(x => x.Id)
                .FirstAsync();
            lineItemAuditId = await setupContext.AuditLogs
                .Where(x => x.EntityType == nameof(ExpenseLineItem))
                .Select(x => x.Id)
                .FirstAsync();
        }

        var service = new AuditService(new TestContextFactory(options, currentUser), currentUser);
        var results = await service.SearchAsync(new SearchAuditLogsRequest(), CancellationToken.None);

        results.Single(x => x.Id == expenseAuditId)
            .EntityContext.Should().Contain("Zakupy w Lidlu").And.Contain("Dom");
        results.Single(x => x.Id == lineItemAuditId)
            .EntityContext.Should().Contain("Płatność za elewację").And.Contain("Zakupy w Lidlu");
    }

    [Fact]
    public async Task AuditService_Should_ShowNamesForForeignKeysAndMonthNamesInDiff()
    {
        var currentUser = CreateCurrentUserContext("admin-user", "owner-user");
        var options = NewOptions(currentUser);

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            setupContext.Users.AddRange(
                new User
                {
                    Id = "admin-user",
                    Username = "Admin",
                    PasswordHash = "hash",
                    IsAdmin = true,
                    BudgetOwnerUserId = "owner-user"
                },
                new User
                {
                    Id = "owner-user",
                    Username = "Owner",
                    PasswordHash = "hash",
                    BudgetOwnerUserId = "owner-user"
                });
            setupContext.Categories.Add(new Category { Name = "Dom", Color = "#123456" });
            setupContext.Accounts.Add(new Account { Name = "Konto rodzinne", Type = 1, Order = 1 });
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 5 });
            await setupContext.SaveChangesAsync();

            setupContext.Incomes.Add(new Income
            {
                Year = 2026,
                Month = 5,
                Name = "Wynagrodzenie",
                Amount = 5000,
                AccountId = await setupContext.Accounts.Select(x => x.Id).SingleAsync(),
                ExpectedDayOfMonth = new DateOnly(2026, 5, 10)
            });

            setupContext.Expenses.Add(new Expense
            {
                MonthPlanId = await setupContext.MonthPlans.Select(x => x.Id).SingleAsync(),
                Name = "Elewacja",
                CategoryId = await setupContext.Categories.Select(x => x.Id).SingleAsync(),
                PlannedAmount = 1000,
                ActualAmount = 0,
                Order = 1
            });
            await setupContext.SaveChangesAsync();
        }

        var service = new AuditService(new TestContextFactory(options, currentUser), currentUser);
        var results = await service.SearchAsync(new SearchAuditLogsRequest(), CancellationToken.None);

        var incomeDiff = results.Single(x => x.EntityType == nameof(Income)).DiffItems;
        incomeDiff.Single(x => x.PropertyName == "AccountId").NewValue.Should().Be("Konto rodzinne");
        incomeDiff.Single(x => x.PropertyName == "Month").NewValue.Should().Be("maj");
        incomeDiff.Single(x => x.PropertyName == "Year").NewValue.Should().Be("2026");

        var expenseDiff = results.Single(x => x.EntityType == nameof(Expense)).DiffItems;
        expenseDiff.Single(x => x.PropertyName == "CategoryId").NewValue.Should().Be("Dom");
        expenseDiff.Single(x => x.PropertyName == "MonthPlanId").NewValue.Should().Be("maj 2026");
    }

    [Fact]
    public async Task SaveChanges_Should_AuditSavingsAndRecurringDefinitions()
    {
        var currentUser = CreateCurrentUserContext("actor-user", "owner-user");
        var options = NewOptions(currentUser);

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            SeedUsers(setupContext);
            setupContext.Categories.Add(new Category { Name = "Dom", Color = "#123456" });
            setupContext.Accounts.Add(new Account { Name = "Konto rodzinne", Type = 1, Order = 1 });
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 5 });
            await setupContext.SaveChangesAsync();

            setupContext.MonthSavingsTransferItems.Add(new MonthSavingsTransferItem
            {
                MonthPlanId = await setupContext.MonthPlans.Select(x => x.Id).SingleAsync(),
                Amount = 500,
                TransferDate = new DateOnly(2026, 5, 15)
            });
            setupContext.RegularExpenseDefinitions.Add(new RegularExpenseDefinition
            {
                Name = "Internet",
                CategoryId = await setupContext.Categories.Select(x => x.Id).SingleAsync(),
                Amount = 80,
                Order = 1
            });
            setupContext.RegularIncomeDefinitions.Add(new RegularIncomeDefinition
            {
                Name = "Pensja",
                AccountId = await setupContext.Accounts.Select(x => x.Id).SingleAsync(),
                Amount = 5000,
                DayOfMonth = 10
            });
            await setupContext.SaveChangesAsync();
        }

        await using var verifyContext = new ApplicationDbContext(options, currentUser);
        var entityTypes = await verifyContext.AuditLogs
            .Select(x => x.EntityType)
            .ToListAsync();

        entityTypes.Should().Contain(nameof(MonthSavingsTransferItem));
        entityTypes.Should().Contain(nameof(RegularExpenseDefinition));
        entityTypes.Should().Contain(nameof(RegularIncomeDefinition));
    }

    [Fact]
    public async Task AuditService_Should_ReturnOnlyCurrentBudgetOwnerLogsForAdmin()
    {
        var currentUser = CreateCurrentUserContext("admin-user", "owner-user");
        var options = NewOptions(currentUser);

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            setupContext.Users.AddRange(
                new User
                {
                    Id = "admin-user",
                    Username = "Admin",
                    PasswordHash = "hash",
                    IsAdmin = true,
                    BudgetOwnerUserId = "owner-user"
                },
                new User
                {
                    Id = "owner-user",
                    Username = "Owner",
                    PasswordHash = "hash",
                    BudgetOwnerUserId = "owner-user"
                });
            setupContext.AuditLogs.AddRange(
                new AuditLog
                {
                    EntityType = nameof(Expense),
                    EntityId = 1,
                    UserId = "admin-user",
                    BudgetOwnerUserId = "owner-user",
                    Operation = "Update",
                    OldValuesJson = """{"ActualAmount":10}""",
                    NewValuesJson = """{"ActualAmount":20}""",
                    ChangedAtUtc = DateTime.UtcNow
                },
                new AuditLog
                {
                    EntityType = nameof(Expense),
                    EntityId = 2,
                    UserId = "admin-user",
                    BudgetOwnerUserId = "other-owner",
                    Operation = "Update",
                    OldValuesJson = "{}",
                    NewValuesJson = "{}",
                    ChangedAtUtc = DateTime.UtcNow
                });
            await setupContext.SaveChangesAsync();
        }

        var service = new AuditService(new TestContextFactory(options, currentUser), currentUser);
        var results = await service.SearchAsync(new SearchAuditLogsRequest(), CancellationToken.None);

        results.Should().ContainSingle();
        results[0].EntityId.Should().Be(1);
        results[0].DiffItems.Should().ContainSingle(x => x.PropertyName == "ActualAmount");
    }

    [Fact]
    public async Task AuditService_WhenCurrentUserIsNotAdmin_Should_ThrowForbidden()
    {
        var currentUser = CreateCurrentUserContext("standard-user", "owner-user");
        var options = NewOptions(currentUser);

        await using (var setupContext = new ApplicationDbContext(options, currentUser))
        {
            setupContext.Users.Add(new User
            {
                Id = "standard-user",
                Username = "Standard",
                PasswordHash = "hash",
                IsAdmin = false,
                BudgetOwnerUserId = "owner-user"
            });
            await setupContext.SaveChangesAsync();
        }

        var service = new AuditService(new TestContextFactory(options, currentUser), currentUser);

        await service.Invoking(x => x.SearchAsync(new SearchAuditLogsRequest(), CancellationToken.None))
            .Should()
            .ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task SaveChanges_WhenNoSessionExists_Should_Reject_AuditedChanges()
    {
        var currentUser = new CurrentUserContext();
        var options = NewOptions(currentUser);

        await using var dbContext = new ApplicationDbContext(options, currentUser);
        dbContext.Categories.Add(new Category { Name = "Hidden", Color = "#123456" });

        await dbContext.Invoking(x => x.SaveChangesAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*authenticated user or explicit system scope*");
    }

    [Fact]
    public async Task SaveChanges_WhenSystemOperation_Should_Not_Create_AuditLogs()
    {
        var currentUser = CurrentUserContext.ForTechnicalOwner();
        var options = NewOptions(currentUser);

        await using var dbContext = new ApplicationDbContext(options, currentUser);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = User.TechnicalOwnerUsername,
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId
        });
        dbContext.Accounts.Add(new Account { Name = "System restore account", Type = 1, Order = 1 });

        await dbContext.SaveChangesAsync();

        (await dbContext.AuditLogs.CountAsync()).Should().Be(0);
    }

    private static DbContextOptions<ApplicationDbContext> NewOptions(CurrentUserContext currentUserContext)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditSaveChangesInterceptor(currentUserContext))
            .Options;
    }

    private static CurrentUserContext CreateCurrentUserContext(string userId, string budgetOwnerUserId)
    {
        return new CurrentUserContext { UserId = userId, BudgetOwnerUserId = budgetOwnerUserId };
    }

    private static void SeedUsers(ApplicationDbContext dbContext)
    {
        dbContext.Users.AddRange(
            new User
            {
                Id = "actor-user",
                Username = "Actor",
                PasswordHash = "hash",
                IsAdmin = true,
                BudgetOwnerUserId = "owner-user"
            },
            new User
            {
                Id = "owner-user",
                Username = "Owner",
                PasswordHash = "hash",
                IsAdmin = true,
                BudgetOwnerUserId = "owner-user"
            });
    }

    private static Dictionary<string, JsonElement> DeserializeValues(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
    }

    private sealed class TestContextFactory(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUserContext) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            return new ApplicationDbContext(options, currentUserContext);
        }

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
