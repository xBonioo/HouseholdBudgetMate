using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HouseholdBudgetMate.Tests.Tests.Infrastructure;

public sealed class CategorySeedServiceTests
{
    [Fact]
    public async Task SeedDefaultCategoriesAsync_Should_Be_Idempotent()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var factory = new TestDbContextFactoryForSeed(options);
        var service = new CategorySeedService(factory, NullLogger<CategorySeedService>.Instance);

        await service.SeedDefaultCategoriesAsync(CancellationToken.None);
        await service.SeedDefaultCategoriesAsync(CancellationToken.None);

        await using var context = new ApplicationDbContext(options);
        var categories = await context.Categories.IgnoreQueryFilters().OrderBy(x => x.Name).ToListAsync();

        Assert.Equal(5, categories.Count);
        Assert.Contains(categories, x => x.Name == "Spożywcze");
        Assert.Contains(categories, x => x.Name == "Transport");
        Assert.Contains(categories, x => x.Name == "Zdrowie");
        Assert.Contains(categories, x => x.Name == "Rozrywka");
        Assert.Contains(categories, x => x.Name == "Dom");
    }

    private sealed class TestDbContextFactoryForSeed(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}

