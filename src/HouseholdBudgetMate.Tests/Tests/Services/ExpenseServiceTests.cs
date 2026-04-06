using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class ExpenseServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private ExpenseService CreateService()
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(DateTime.UtcNow);
        return new ExpenseService(factory, provider);
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
}
