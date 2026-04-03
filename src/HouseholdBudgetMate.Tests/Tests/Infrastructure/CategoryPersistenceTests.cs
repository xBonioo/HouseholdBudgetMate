using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Infrastructure;

public sealed class CategoryPersistenceTests
{
    [Fact]
    public async Task Categories_Query_Should_Exclude_SoftDeleted_Records_By_Default()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            await context.Categories.AddRangeAsync(
                new Category { Name = "Spożywcze", Color = "#4CAF50", IsDeleted = false },
                new Category { Name = "Transport", Color = "#1E88E5", IsDeleted = true, DeletedAtUtc = DateTime.UtcNow });

            await context.SaveChangesAsync();
        }

        await using (var context = new ApplicationDbContext(options))
        {
            var visibleCategories = await context.Categories.ToListAsync();
            var allCategories = await context.Categories.IgnoreQueryFilters().ToListAsync();

            Assert.Single(visibleCategories);
            Assert.Equal(2, allCategories.Count);
            Assert.Equal("Spożywcze", visibleCategories[0].Name);
        }
    }
}

