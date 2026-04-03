using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Infrastructure;

public sealed class TagPersistenceTests
{
    [Fact]
    public async Task Tags_Query_Should_Exclude_SoftDeleted_Records_By_Default()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            var category = new Category { Name = "Spozywcze", Color = "#4CAF50" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            await context.Tags.AddRangeAsync(
                new Tag { Name = "Lidl", CategoryId = category.Id, IsDeleted = false },
                new Tag { Name = "Biedronka", CategoryId = category.Id, IsDeleted = true, DeletedAtUtc = DateTime.UtcNow });

            await context.SaveChangesAsync();
        }

        await using (var context = new ApplicationDbContext(options))
        {
            var visibleTags = await context.Tags.ToListAsync();
            var allTags = await context.Tags.IgnoreQueryFilters().ToListAsync();

            Assert.Single(visibleTags);
            Assert.Equal(2, allTags.Count);
            Assert.Equal("Lidl", visibleTags[0].Name);
        }
    }
}

