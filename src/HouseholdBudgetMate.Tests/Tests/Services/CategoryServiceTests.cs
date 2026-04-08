using HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class CategoryServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    
    private CategoryService CreateService()
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(DateTime.UtcNow);
        return new CategoryService(factory, provider);
    }
    
    [Fact]
    public async Task CreateCategoryAsync_Should_Reject_Duplicate_Name_Ignoring_Case()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Categories.Add(new Category { Name = "Spozywcze", Color = "#111111" });
            await context.SaveChangesAsync();
        }

        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name = "  spozywcze  ",
            Color = "#111111"
        }, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteCategoryAsync_Should_SoftDelete_Category_And_Its_Tags()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#1E88E5" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            context.Tags.Add(new Tag { Name = "Auto", CategoryId = category.Id });
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        await service.DeleteCategoryAsync(new DeleteCategoryRequest { Id = categoryId }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);

        var deletedCategory = await verifyContext.Categories.IgnoreQueryFilters().SingleAsync(x => x.Id == categoryId);
        var deletedTag = await verifyContext.Tags.IgnoreQueryFilters().SingleAsync(x => x.CategoryId == categoryId);

        Assert.True(deletedCategory.IsDeleted);
        Assert.NotNull(deletedCategory.DeletedAtUtc);
        Assert.True(deletedTag.IsDeleted);
        Assert.NotNull(deletedTag.DeletedAtUtc);
    }

    [Fact]
    public async Task CreateTagAsync_Should_Assign_ParentTagId_When_Provided()
    {
        int categoryId;
        int parentTagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#22AA22" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var parentTag = new Tag { Name = "Spozywcze", CategoryId = categoryId };
            context.Tags.Add(parentTag);
            await context.SaveChangesAsync();
            parentTagId = parentTag.Id;
        }

        var service = CreateService();
        var created = await service.CreateTagAsync(new CreateTagRequest
        {
            CategoryId = categoryId,
            Name = "Lidl",
            ParentTagId = parentTagId
        }, CancellationToken.None);

        Assert.Equal(parentTagId, created.ParentTagId);
    }

    [Fact]
    public async Task UpdateTagAsync_Should_Reject_Parent_That_Is_Already_Child_Tag()
    {
        int categoryId;
        int parentTagId;
        int childTagId;
        int tagToUpdateId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Dom", Color = "#336699" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var parent = new Tag { Name = "Zakupy", CategoryId = categoryId };
            context.Tags.Add(parent);
            await context.SaveChangesAsync();
            parentTagId = parent.Id;

            var child = new Tag { Name = "Biedronka", CategoryId = categoryId, ParentTagId = parentTagId };
            var independent = new Tag { Name = "Media", CategoryId = categoryId };
            context.Tags.AddRange(child, independent);
            await context.SaveChangesAsync();
            childTagId = child.Id;
            tagToUpdateId = independent.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateTagAsync(new UpdateTagRequest
        {
            Id = tagToUpdateId,
            CategoryId = categoryId,
            Name = "Media",
            ParentTagId = childTagId
        }, CancellationToken.None));
    }
}
