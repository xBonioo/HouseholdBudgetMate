using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HouseholdBudgetMate.Application.Services;

public sealed class CategorySeedService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<CategorySeedService> logger)
{
    private static readonly IReadOnlyList<Category> DefaultCategories =
    [
        new() { Name = "Spożywcze", Color = "#4CAF50", SupportsLineItems = true },
        new() { Name = "Samochód", Color = "#1E88E5", SupportsLineItems = true },
        new() { Name = "Zdrowie", Color = "#E53935", SupportsLineItems = false },
        new() { Name = "Rozrywka", Color = "#8E24AA", SupportsLineItems = true },
        new() { Name = "Dom", Color = "#FB8C00", SupportsLineItems = false }
    ];

    public async Task SeedDefaultCategoriesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var hasAnyCategory = await dbContext.Categories
            .IgnoreQueryFilters()
            .AnyAsync(cancellationToken);

        if (hasAnyCategory)
        {
            return;
        }

        await dbContext.Categories.AddRangeAsync(DefaultCategories, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Default categories seeded.");
    }
}


