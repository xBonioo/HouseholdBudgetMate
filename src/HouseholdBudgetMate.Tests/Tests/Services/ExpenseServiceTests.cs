using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Facility.Events;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class ExpenseServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private ExpenseService CreateService(RecordingAppEventPublisher? eventPublisher = null)
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(DateTime.UtcNow);
        return new ExpenseService(
            factory,
            provider,
            eventPublisher ?? new RecordingAppEventPublisher(),
            new NoOpIncomeService(),
            new NoOpLoanService());
    }

    [Fact]
    public async Task CreateExpenseAsync_Should_Emit_BudgetExceededEvent_When_Category_Limit_Is_Crossed()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category
            {
                Name = "Spozywcze",
                Color = "#43A047",
                EnvelopeLimit = 500m
            };

            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var publisher = new RecordingAppEventPublisher();
        var service = CreateService(publisher);

        await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Zakupy 1",
            CategoryId = categoryId,
            PlannedAmount = 300m,
            ActualAmount = 300m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Zakupy 2",
            CategoryId = categoryId,
            PlannedAmount = 210m,
            ActualAmount = 210m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var budgetEvents = publisher.Events.OfType<BudgetExceededEvent>().ToList();

        Assert.Single(budgetEvents);
        Assert.Equal(categoryId, budgetEvents[0].CategoryId);
        Assert.Equal(510m, budgetEvents[0].SpentAmount);
        Assert.Equal(500m, budgetEvents[0].EnvelopeLimit);
    }

    [Fact]
    public async Task GetMonthAsync_Should_Create_MonthPlan_When_Missing()
    {
        var service = CreateService();

        var result = await service.GetMonthAsync(2026, 4, CancellationToken.None);

        Assert.Equal(2026, result.Year);
        Assert.Equal(4, result.Month);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var monthPlans = await verifyContext.MonthPlans.ToListAsync();
        Assert.Single(monthPlans);
    }

    [Fact]
    public async Task DeleteExpenseAsync_Should_SoftDelete_Expense()
    {
        int expenseId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#1E88E5" };
            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.Categories.Add(category);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Paliwo",
                CategoryId = category.Id,
                PlannedAmount = 300,
                ActualAmount = 250
            };

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;
        }

        var service = CreateService();
        await service.DeleteExpenseAsync(new DeleteExpenseRequest { Id = expenseId }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var deleted = await verifyContext.Expenses.IgnoreQueryFilters().SingleAsync(x => x.Id == expenseId);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAtUtc);
    }

    [Fact]
    public async Task SavingsTransferItems_Crud_Should_Work()
    {
        var service = CreateService();

        var created = await service.CreateMonthSavingsTransferItemAsync(new CreateMonthSavingsTransferItemRequest
        {
            Year = 2026,
            Month = 4,
            Amount = 300m,
            TransferDate = new DateOnly(2026, 4, 10)
        }, CancellationToken.None);

        var updated = await service.UpdateMonthSavingsTransferItemAsync(new UpdateMonthSavingsTransferItemRequest
        {
            Id = created.Id,
            Amount = 350m,
            TransferDate = new DateOnly(2026, 4, 12)
        }, CancellationToken.None);

        Assert.Equal(350m, updated.Amount);

        await service.DeleteMonthSavingsTransferItemAsync(new DeleteMonthSavingsTransferItemRequest { Id = created.Id }, CancellationToken.None);

        var month = await service.GetMonthAsync(2026, 4, CancellationToken.None);
        Assert.Empty(month.SavingsTransfers);
    }

    [Fact]
    public async Task GetMonthAsync_Should_Calculate_Kpi_With_Remaining_Fallback_Without_Double_Counting()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Dom", Color = "#43A047" };
            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.Categories.Add(category);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            context.Expenses.AddRange(
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "A",
                    CategoryId = category.Id,
                    PlannedAmount = 100m,
                    ActualAmount = 40m,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "B",
                    CategoryId = category.Id,
                    PlannedAmount = 200m,
                    ActualAmount = 0m,
                    ShowRemainingInUI = false
                },
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "C",
                    CategoryId = category.Id,
                    PlannedAmount = 300m,
                    ActualAmount = 0,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "D",
                    CategoryId = category.Id,
                    PlannedAmount = 0,
                    ActualAmount = 50m,
                    ShowRemainingInUI = true
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var month = await service.GetMonthAsync(2026, 4, CancellationToken.None);

        Assert.Equal(600m, month.Kpi.PlannedTotal);
        Assert.Equal(90m, month.Kpi.SpentTotal);
        Assert.Equal(560m, month.Kpi.RemainingTotal);
        Assert.Equal(93.33d, month.Kpi.RemainingPercent, 2);
    }

    [Fact]
    public async Task CreateExpenseAsync_Should_Assign_Last_Order_In_Month()
    {
        int categoryId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Rachunki", Color = "#455A64" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();

        var first = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 5,
            Name = "A",
            CategoryId = categoryId,
            PlannedAmount = 100m,
            ActualAmount = 10m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var second = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 5,
            Name = "B",
            CategoryId = categoryId,
            PlannedAmount = 200m,
            ActualAmount = 20m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        Assert.Equal(1, first.Order);
        Assert.Equal(2, second.Order);
    }

    [Fact]
    public async Task ReorderExpensesAsync_Should_Persist_New_Order()
    {
        int categoryId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Zakupy", Color = "#6D4C41" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var first = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 6,
            Name = "Pierwszy",
            CategoryId = categoryId,
            PlannedAmount = 50m,
            ActualAmount = 0,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var second = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 6,
            Name = "Drugi",
            CategoryId = categoryId,
            PlannedAmount = 80m,
            ActualAmount = 0,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        await service.ReorderExpensesAsync(new ReorderExpensesRequest
        {
            ExpenseIds = [second.Id, first.Id]
        }, CancellationToken.None);

        var month = await service.GetMonthAsync(2026, 6, CancellationToken.None);
        Assert.Equal(second.Id, month.Expenses[0].Id);
        Assert.Equal(first.Id, month.Expenses[1].Id);
    }

    [Fact]
    public async Task CreateExpenseAsync_Should_Throw_When_Month_Is_Closed()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#1E88E5" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        await service.CloseMonthAsync(2026, 7, CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 7,
            Name = "Paliwo",
            CategoryId = categoryId,
            PlannedAmount = 150m,
            ActualAmount = 0,
            ShowRemainingInUI = true
        }, CancellationToken.None));
    }

    [Fact]
    public async Task CloseMonthAsync_Should_Generate_Regular_Expenses_In_Next_Month_And_Be_Idempotent()
    {
        int categoryId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        await service.CloseMonthAsync(2026, 1, CancellationToken.None);
        await service.OpenMonthAsync(2026, 2, CancellationToken.None);

        var february = await service.GetMonthAsync(2026, 2, CancellationToken.None);
        var recurringExpenses = february.Expenses.Where(x => x.Name == "Netflix").ToList();

        Assert.Single(recurringExpenses);
        Assert.Equal(60m, recurringExpenses[0].PlannedAmount);
    }

    [Fact]
    public async Task GetMonthAsync_Should_AutoSync_Recurring_Data_For_Open_Month()
    {
        int categoryId;
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            context.Categories.Add(category);
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            categoryId = category.Id;
            accountId = account.Id;
        }

        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var now = DateTime.UtcNow;
        var provider = new StaticDateTimeProvider(now);
        var incomeService = new IncomeService(factory, provider);
        var expenseService = new ExpenseService(
            factory,
            provider,
            new RecordingAppEventPublisher(),
            incomeService,
            new NoOpLoanService());

        await expenseService.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        await incomeService.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);

        var month = await expenseService.GetMonthAsync(2026, 3, CancellationToken.None);
        var incomes = await incomeService.GetMonthIncomesAsync(2026, 3, CancellationToken.None);

        Assert.Contains(month.Expenses, x => x.Name == "Netflix" && x.PlannedAmount == 60m);
        Assert.Contains(incomes, x => x.Name == "Wyplata" && x.IsRegular);
    }

    [Fact]
    public async Task DeleteRegularExpenseDefinitionAsync_Should_SoftDelete_By_Setting_IsActive_False()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Rachunki", Color = "#455A64" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var created = await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Internet",
            CategoryId = categoryId,
            Amount = 80m
        }, CancellationToken.None);

        await service.DeleteRegularExpenseDefinitionAsync(new DeleteRegularExpenseDefinitionRequest { Id = created.Id }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var definition = await verifyContext.RegularExpenseDefinitions.FirstAsync(x => x.Id == created.Id);
        Assert.False(definition.IsActive);
    }

    [Fact]
    public async Task DeleteRegularExpenseDefinitionPermanentlyAsync_Should_Remove_Definition_From_Database()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Rachunki", Color = "#455A64" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var created = await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Internet",
            CategoryId = categoryId,
            Amount = 80m
        }, CancellationToken.None);

        await service.DeleteRegularExpenseDefinitionPermanentlyAsync(
            new DeleteRegularExpenseDefinitionRequest { Id = created.Id },
            CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var exists = await verifyContext.RegularExpenseDefinitions.AnyAsync(x => x.Id == created.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task ReorderRegularExpenseDefinitionsAsync_Should_Drive_Order_Of_AutoGenerated_Expenses()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Rachunki", Color = "#455A64" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var first = await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Internet",
            CategoryId = categoryId,
            Amount = 80m
        }, CancellationToken.None);

        var second = await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        await service.ReorderRegularExpenseDefinitionsAsync(new ReorderRegularExpenseDefinitionsRequest
        {
            DefinitionIds = [second.Id, first.Id]
        }, CancellationToken.None);

        await service.OpenMonthAsync(2026, 9, CancellationToken.None);
        var month = await service.GetMonthAsync(2026, 9, CancellationToken.None);

        Assert.Equal("Netflix", month.Expenses[0].Name);
        Assert.Equal("Internet", month.Expenses[1].Name);
    }

    [Fact]
    public async Task DeleteRecurringExpense_FromMonth_Should_Not_Recreate_And_Should_Not_Throw_On_Reload()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        var initialMonth = await service.GetMonthAsync(2026, 10, CancellationToken.None);
        var recurringExpense = Assert.Single(initialMonth.Expenses, x => x.Name == "Netflix");

        await service.DeleteExpenseAsync(new DeleteExpenseRequest { Id = recurringExpense.Id }, CancellationToken.None);

        var reloadedMonth = await service.GetMonthAsync(2026, 10, CancellationToken.None);
        Assert.DoesNotContain(reloadedMonth.Expenses, x => x.Name == "Netflix");

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var storedExpenses = await verifyContext.Expenses
            .IgnoreQueryFilters()
            .Where(x => x.MonthPlanId == reloadedMonth.Id)
            .ToListAsync();

        Assert.Single(storedExpenses);
        Assert.True(storedExpenses[0].IsDeleted);
    }

    [Fact]
    public async Task DeleteRecurringExpense_FromMonth_Should_Still_Generate_In_Next_Month()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        var october = await service.GetMonthAsync(2026, 10, CancellationToken.None);
        var recurringExpense = Assert.Single(october.Expenses, x => x.Name == "Netflix");
        await service.DeleteExpenseAsync(new DeleteExpenseRequest { Id = recurringExpense.Id }, CancellationToken.None);

        await service.OpenMonthAsync(2026, 11, CancellationToken.None);
        var november = await service.GetMonthAsync(2026, 11, CancellationToken.None);

        Assert.Contains(november.Expenses, x => x.Name == "Netflix" && x.PlannedAmount == 60m);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_Should_Calculate_Month_And_Ytd_Metrics()
    {
        int categoryId;
        int accountId;
        int savingsAccountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#43A047" };
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            var savingsAccount = new Account { Name = "Skarbonka", Type = (int)AccountType.Savings };
            context.Categories.Add(category);
            context.Accounts.Add(account);
            context.Accounts.Add(savingsAccount);
            await context.SaveChangesAsync();
            categoryId = category.Id;
            accountId = account.Id;
            savingsAccountId = savingsAccount.Id;

            var january = new MonthPlan { Year = 2026, Month = 1 };
            var february = new MonthPlan { Year = 2026, Month = 2 };
            context.MonthPlans.AddRange(january, february);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance
                {
                    AccountId = accountId,
                    Year = 2025,
                    Month = 12,
                    ClosingBalance = 900m
                },
                new AccountMonthBalance
                {
                    AccountId = accountId,
                    Year = 2026,
                    Month = 1,
                    ClosingBalance = 1000m
                },
                new AccountMonthBalance
                {
                    AccountId = accountId,
                    Year = 2026,
                    Month = 2,
                    ClosingBalance = 1100m
                },
                new AccountMonthBalance
                {
                    AccountId = savingsAccountId,
                    Year = 2025,
                    Month = 12,
                    ClosingBalance = 150m
                },
                new AccountMonthBalance
                {
                    AccountId = savingsAccountId,
                    Year = 2026,
                    Month = 1,
                    ClosingBalance = 200m
                },
                new AccountMonthBalance
                {
                    AccountId = savingsAccountId,
                    Year = 2026,
                    Month = 2,
                    ClosingBalance = 260m
                });

            context.Expenses.AddRange(
                new Expense
                {
                    MonthPlanId = january.Id,
                    Order = 1,
                    Name = "Zakupy styczen",
                    CategoryId = categoryId,
                    PlannedAmount = 100m,
                    ActualAmount = 60m,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = february.Id,
                    Order = 1,
                    Name = "Zakupy luty",
                    CategoryId = categoryId,
                    PlannedAmount = 200m,
                    ActualAmount = 250m,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = february.Id,
                    Order = 2,
                    Name = "Nieplanowany",
                    CategoryId = categoryId,
                    PlannedAmount = 0m,
                    ActualAmount = 30m,
                    ShowRemainingInUI = true
                });

            context.Incomes.AddRange(
                new Income
                {
                    Year = 2026,
                    Month = 1,
                    Name = "Wyplata 1",
                    Amount = 500m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, 1, 10)
                },
                new Income
                {
                    Year = 2026,
                    Month = 2,
                    Name = "Wyplata 2",
                    Amount = 600m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, 2, 10)
                });

            context.MonthSavingsTransferItems.Add(new MonthSavingsTransferItem
            {
                MonthPlanId = february.Id,
                Amount = 200m,
                TransferDate = new DateOnly(2026, 2, 12)
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var summary = await service.GetDashboardSummaryAsync(2026, 2, CancellationToken.None);

        Assert.Equal(4, summary.TransactionCount);
        Assert.Equal(30m, summary.UnplannedSpentTotal);
        Assert.Equal(160m, summary.SavedAmountThisMonth);
        Assert.Equal(310m, summary.SavedAmountYearToDate);
        Assert.Equal(550m, summary.AverageMonthlyIncome);
        Assert.Equal(170m, summary.AverageMonthlySpent);
        Assert.Equal(155m, summary.AverageMonthlySaved);
        Assert.Equal(2, summary.SavingsTimeline.Count);
        Assert.Equal(150m, summary.SavingsTimeline.Single(x => x.Month == 1).SavedAmount);
        Assert.Equal(160m, summary.SavingsTimeline.Single(x => x.Month == 2).SavedAmount);
    }
}
