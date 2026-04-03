using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Infrastructure;

public sealed class ExpensePersistenceTests
{
    [Fact]
    public async Task Expenses_Query_Should_Exclude_SoftDeleted_Records_By_Default()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            var category = new Category { Name = "Spozywcze", Color = "#4CAF50" };
            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.Categories.Add(category);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            await context.Expenses.AddRangeAsync(
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "Zakupy",
                    CategoryId = category.Id,
                    PlannedAmount = 100,
                    ActualAmount = 80,
                    IsDeleted = false
                },
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "Paliwo",
                    CategoryId = category.Id,
                    PlannedAmount = 200,
                    ActualAmount = 210,
                    IsDeleted = true,
                    DeletedAtUtc = DateTime.UtcNow
                });

            await context.SaveChangesAsync();
        }

        await using (var context = new ApplicationDbContext(options))
        {
            var visible = await context.Expenses.ToListAsync();
            var all = await context.Expenses.IgnoreQueryFilters().ToListAsync();

            Assert.Single(visible);
            Assert.Equal(2, all.Count);
            Assert.Equal("Zakupy", visible[0].Name);
        }
    }
}

