using HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class CategoryServiceTests
{
    [Fact]
    public async Task CreateCategoryAsync_Should_Reject_Duplicate_Name_Ignoring_Case()
    {
        var options = CreateOptions();

        await using (var context = new ApplicationDbContext(options))
        {
            context.Categories.Add(new Category { Name = "Spozywcze", Color = "#4CAF50" });
            await context.SaveChangesAsync();
        }

        var service = new CategoryService(new TestDbContextFactory(options));

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name = "  spozywcze  ",
            Color = "#111111"
        }, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteCategoryAsync_Should_SoftDelete_Category_And_Its_Tags()
    {
        var options = CreateOptions();

        int categoryId;

        await using (var context = new ApplicationDbContext(options))
        {
            var category = new Category { Name = "Transport", Color = "#1E88E5" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            context.Tags.Add(new Tag { Name = "Auto", CategoryId = category.Id });
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = new CategoryService(new TestDbContextFactory(options));
        await service.DeleteCategoryAsync(new DeleteCategoryRequest { Id = categoryId }, CancellationToken.None);

        await using var verifyContext = new ApplicationDbContext(options);

        var deletedCategory = await verifyContext.Categories.IgnoreQueryFilters().SingleAsync(x => x.Id == categoryId);
        var deletedTag = await verifyContext.Tags.IgnoreQueryFilters().SingleAsync(x => x.CategoryId == categoryId);

        Assert.True(deletedCategory.IsDeleted);
        Assert.NotNull(deletedCategory.DeletedAtUtc);
        Assert.True(deletedTag.IsDeleted);
        Assert.NotNull(deletedTag.DeletedAtUtc);
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}

