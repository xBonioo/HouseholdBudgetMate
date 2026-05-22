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
    private static readonly DateTime DefaultNowUtc = new(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);

    private CategoryService CreateService(DateTime? nowUtc = null)
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(nowUtc ?? DefaultNowUtc);
        return new CategoryService(factory, provider);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that GetAllAsync returns all active categories ordered alphabetically by name.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_Should_Return_Categories_Ordered_By_Name()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Categories.AddRange(
                new Category { Name = "Zdrowie", Color = "#FF0000" },
                new Category { Name = "Auto", Color = "#00FF00" },
                new Category { Name = "Dom", Color = "#0000FF" });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal("Auto", result[0].Name);
        Assert.Equal("Dom", result[1].Name);
        Assert.Equal("Zdrowie", result[2].Name);
    }

    /// <summary>
    /// Verifies that soft-deleted categories are excluded from GetAllAsync results.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_Should_Exclude_Deleted_Categories()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Categories.AddRange(
                new Category { Name = "Active", Color = "#111111" },
                new Category { Name = "Deleted", Color = "#222222", IsDeleted = true, DeletedAtUtc = DefaultNowUtc });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Active", result[0].Name);
    }

    /// <summary>
    /// Verifies that tags belonging to a category are included in the GetAllAsync result.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_Should_Include_Tags_Within_Category()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            context.Tags.AddRange(
                new Tag { Name = "Biedronka", CategoryId = category.Id },
                new Tag { Name = "Lidl", CategoryId = category.Id });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(2, result[0].Tags.Count);
    }

    // ── CreateCategoryAsync ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that CreateCategoryAsync persists all provided fields correctly.
    /// </summary>
    [Fact]
    public async Task CreateCategoryAsync_Should_Persist_All_Fields()
    {
        var service = CreateService();
        var result = await service.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name = "Rozrywka",
            Color = "#FF5500",
            EnvelopeLimit = 500m,
            SupportsLineItems = true
        }, CancellationToken.None);

        await using var context = TestDbContextFactory.CreateDbContext(_dbName);
        var saved = await context.Categories.FindAsync(result.Id);

        Assert.NotNull(saved);
        Assert.Equal("Rozrywka", saved!.Name);
        Assert.Equal("#FF5500", saved.Color);
        Assert.Equal(500m, saved.EnvelopeLimit);
        Assert.True(saved.SupportsLineItems);
    }

    /// <summary>
    /// Verifies that CreateCategoryAsync trims whitespace and rejects a duplicate name (case-insensitive).
    /// </summary>
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

    /// <summary>
    /// Verifies that a category with the same name as a soft-deleted category can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateCategoryAsync_Should_Allow_Name_Of_Deleted_Category()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Categories.Add(new Category
            {
                Name = "Transport",
                Color = "#000000",
                IsDeleted = true,
                DeletedAtUtc = DefaultNowUtc
            });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        // Should not throw ConflictException
        var result = await service.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name = "Transport",
            Color = "#FFFFFF"
        }, CancellationToken.None);

        Assert.Equal("Transport", result.Name);
    }

    /// <summary>
    /// Verifies that CreateCategoryAsync throws BadRequestException when Name is empty.
    /// </summary>
    [Fact]
    public async Task CreateCategoryAsync_Should_Throw_BadRequest_When_Name_Is_Empty()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name = "   ",
            Color = "#AABBCC"
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that CreateCategoryAsync throws BadRequestException when EnvelopeLimit is negative.
    /// </summary>
    [Fact]
    public async Task CreateCategoryAsync_Should_Throw_BadRequest_When_EnvelopeLimit_Is_Negative()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name = "Jedzenie",
            Color = "#AABBCC",
            EnvelopeLimit = -1m
        }, CancellationToken.None));
    }

    // ── UpdateCategoryAsync ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that UpdateCategoryAsync persists all updated fields.
    /// </summary>
    [Fact]
    public async Task UpdateCategoryAsync_Should_Persist_Updated_Fields()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "OldName", Color = "#000000" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var result = await service.UpdateCategoryAsync(new UpdateCategoryRequest
        {
            Id = categoryId,
            Name = "NewName",
            Color = "#FFFFFF",
            EnvelopeLimit = 200m,
            SupportsLineItems = true
        }, CancellationToken.None);

        Assert.Equal("NewName", result.Name);
        Assert.Equal("#FFFFFF", result.Color);
        Assert.Equal(200m, result.EnvelopeLimit);
        Assert.True(result.SupportsLineItems);
    }

    /// <summary>
    /// Verifies that UpdateCategoryAsync throws NotFoundException when the category does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateCategoryAsync_Should_Throw_NotFoundException_When_Category_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateCategoryAsync(new UpdateCategoryRequest
        {
            Id = 9999,
            Name = "Ghost",
            Color = "#FFFFFF"
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that a category can be updated with the same name it already has (excludes itself from duplicate check).
    /// </summary>
    [Fact]
    public async Task UpdateCategoryAsync_Should_Allow_Keeping_Same_Name()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Rachunki", Color = "#123456" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var result = await service.UpdateCategoryAsync(new UpdateCategoryRequest
        {
            Id = categoryId,
            Name = "Rachunki",
            Color = "#654321"
        }, CancellationToken.None);

        Assert.Equal("Rachunki", result.Name);
        Assert.Equal("#654321", result.Color);
    }

    /// <summary>
    /// Verifies that UpdateCategoryAsync throws ConflictException when the new name is already taken by another category.
    /// </summary>
    [Fact]
    public async Task UpdateCategoryAsync_Should_Throw_Conflict_When_Name_Taken_By_Another()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Categories.AddRange(
                new Category { Name = "Jedzenie", Color = "#111111" },
                new Category { Name = "Transport", Color = "#222222" });
            await context.SaveChangesAsync();

            categoryId = (await context.Categories.SingleAsync(x => x.Name == "Transport")).Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateCategoryAsync(new UpdateCategoryRequest
        {
            Id = categoryId,
            Name = "Jedzenie",
            Color = "#333333"
        }, CancellationToken.None));
    }

    // ── DeleteCategoryAsync ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that DeleteCategoryAsync soft-deletes the category and all its active tags.
    /// </summary>
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

    /// <summary>
    /// Verifies that DeleteCategoryAsync does not overwrite DeletedAtUtc of tags that were already soft-deleted before the category was deleted.
    /// </summary>
    [Fact]
    public async Task DeleteCategoryAsync_Should_Skip_Already_Deleted_Tags()
    {
        int categoryId;
        var priorDeletedAt = DefaultNowUtc.AddDays(-5);

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Dom", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            // One active tag and one already-deleted tag
            context.Tags.AddRange(
                new Tag { Name = "ActiveTag", CategoryId = categoryId },
                new Tag
                {
                    Name = "PreviouslyDeleted",
                    CategoryId = categoryId,
                    IsDeleted = true,
                    DeletedAtUtc = priorDeletedAt
                });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        await service.DeleteCategoryAsync(new DeleteCategoryRequest { Id = categoryId }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var previouslyDeleted = await verifyContext.Tags
            .IgnoreQueryFilters()
            .SingleAsync(x => x.CategoryId == categoryId && x.Name == "PreviouslyDeleted");

        // DeletedAtUtc must remain unchanged
        Assert.Equal(priorDeletedAt, previouslyDeleted.DeletedAtUtc);
    }

    /// <summary>
    /// Verifies that DeleteCategoryAsync throws NotFoundException when the category does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteCategoryAsync_Should_Throw_NotFoundException_When_Category_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteCategoryAsync(new DeleteCategoryRequest { Id = 9999 }, CancellationToken.None));
    }

    // ── CreateTagAsync ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that CreateTagAsync persists all fields including ParentTagId and SupportsLineItemsOverride.
    /// </summary>
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

    /// <summary>
    /// Verifies that CreateTagAsync throws NotFoundException when the specified category does not exist.
    /// </summary>
    [Fact]
    public async Task CreateTagAsync_Should_Throw_NotFoundException_When_Category_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateTagAsync(new CreateTagRequest
        {
            CategoryId = 9999,
            Name = "SomeTag"
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that CreateTagAsync throws ConflictException when a tag with the same name already exists in the category.
    /// </summary>
    [Fact]
    public async Task CreateTagAsync_Should_Throw_Conflict_When_Tag_Name_Duplicate_In_Category()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            context.Tags.Add(new Tag { Name = "Biedronka", CategoryId = categoryId });
            await context.SaveChangesAsync();
        }

        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateTagAsync(new CreateTagRequest
        {
            CategoryId = categoryId,
            Name = "BIEDRONKA"
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that the same tag name can exist in different categories without conflict.
    /// </summary>
    [Fact]
    public async Task CreateTagAsync_Should_Allow_Same_Tag_Name_In_Different_Categories()
    {
        int category1Id;
        int category2Id;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var cat1 = new Category { Name = "Spozywcze", Color = "#111111" };
            var cat2 = new Category { Name = "Dom", Color = "#222222" };
            context.Categories.AddRange(cat1, cat2);
            await context.SaveChangesAsync();
            category1Id = cat1.Id;
            category2Id = cat2.Id;

            context.Tags.Add(new Tag { Name = "Biedronka", CategoryId = category1Id });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        // Same name in a different category — should not throw
        var result = await service.CreateTagAsync(new CreateTagRequest
        {
            CategoryId = category2Id,
            Name = "Biedronka"
        }, CancellationToken.None);

        Assert.Equal("Biedronka", result.Name);
        Assert.Equal(category2Id, result.CategoryId);
    }

    /// <summary>
    /// Verifies that CreateTagAsync throws NotFoundException when the specified parent tag does not exist.
    /// </summary>
    [Fact]
    public async Task CreateTagAsync_Should_Throw_NotFoundException_When_Parent_Tag_Not_Found()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateTagAsync(new CreateTagRequest
        {
            CategoryId = categoryId,
            Name = "SubTag",
            ParentTagId = 9999
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that CreateTagAsync throws BadRequestException when the parent tag belongs to a different category.
    /// </summary>
    [Fact]
    public async Task CreateTagAsync_Should_Throw_BadRequest_When_Parent_Tag_Belongs_To_Different_Category()
    {
        int category1Id;
        int category2Id;
        int parentTagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var cat1 = new Category { Name = "Spozywcze", Color = "#111111" };
            var cat2 = new Category { Name = "Dom", Color = "#222222" };
            context.Categories.AddRange(cat1, cat2);
            await context.SaveChangesAsync();
            category1Id = cat1.Id;
            category2Id = cat2.Id;

            var parentTag = new Tag { Name = "ParentInCat1", CategoryId = category1Id };
            context.Tags.Add(parentTag);
            await context.SaveChangesAsync();
            parentTagId = parentTag.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateTagAsync(new CreateTagRequest
        {
            CategoryId = category2Id,
            Name = "ChildInCat2",
            ParentTagId = parentTagId
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that CreateTagAsync throws BadRequestException when the parent tag is itself a child tag (only one hierarchy level allowed).
    /// </summary>
    [Fact]
    public async Task CreateTagAsync_Should_Throw_BadRequest_When_Parent_Tag_Is_A_Child_Tag()
    {
        int categoryId;
        int childTagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var parentTag = new Tag { Name = "Parent", CategoryId = categoryId };
            context.Tags.Add(parentTag);
            await context.SaveChangesAsync();

            var childTag = new Tag { Name = "Child", CategoryId = categoryId, ParentTagId = parentTag.Id };
            context.Tags.Add(childTag);
            await context.SaveChangesAsync();
            childTagId = childTag.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateTagAsync(new CreateTagRequest
        {
            CategoryId = categoryId,
            Name = "GrandChild",
            ParentTagId = childTagId
        }, CancellationToken.None));
    }

    // ── UpdateTagAsync ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that UpdateTagAsync persists all updated fields.
    /// </summary>
    [Fact]
    public async Task UpdateTagAsync_Should_Persist_Updated_Fields()
    {
        int categoryId;
        int tagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var tag = new Tag { Name = "OldName", CategoryId = categoryId };
            context.Tags.Add(tag);
            await context.SaveChangesAsync();
            tagId = tag.Id;
        }

        var service = CreateService();
        var result = await service.UpdateTagAsync(new UpdateTagRequest
        {
            Id = tagId,
            CategoryId = categoryId,
            Name = "NewName",
            SupportsLineItemsOverride = true
        }, CancellationToken.None);

        Assert.Equal("NewName", result.Name);
        Assert.True(result.SupportsLineItemsOverride);
    }

    /// <summary>
    /// Verifies that UpdateTagAsync throws NotFoundException when the tag does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateTagAsync_Should_Throw_NotFoundException_When_Tag_Not_Found()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateTagAsync(new UpdateTagRequest
        {
            Id = 9999,
            CategoryId = categoryId,
            Name = "Ghost"
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that UpdateTagAsync throws NotFoundException when the specified category does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateTagAsync_Should_Throw_NotFoundException_When_Category_Not_Found()
    {
        int tagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var tag = new Tag { Name = "SomeTag", CategoryId = category.Id };
            context.Tags.Add(tag);
            await context.SaveChangesAsync();
            tagId = tag.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateTagAsync(new UpdateTagRequest
        {
            Id = tagId,
            CategoryId = 9999,
            Name = "SomeTag"
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that UpdateTagAsync throws BadRequestException when a tag is set as its own parent.
    /// </summary>
    [Fact]
    public async Task UpdateTagAsync_Should_Throw_BadRequest_When_Tag_Set_As_Own_Parent()
    {
        int categoryId;
        int tagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var tag = new Tag { Name = "Auto", CategoryId = categoryId };
            context.Tags.Add(tag);
            await context.SaveChangesAsync();
            tagId = tag.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateTagAsync(new UpdateTagRequest
        {
            Id = tagId,
            CategoryId = categoryId,
            Name = "Auto",
            ParentTagId = tagId // self-reference
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that UpdateTagAsync throws BadRequestException when a parent tag that is already a child tag is specified.
    /// </summary>
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

    // ── DeleteTagAsync ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that DeleteTagAsync soft-deletes the specified tag and sets its DeletedAtUtc timestamp.
    /// </summary>
    [Fact]
    public async Task DeleteTagAsync_Should_SoftDelete_Tag()
    {
        int tagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var tag = new Tag { Name = "Biedronka", CategoryId = category.Id };
            context.Tags.Add(tag);
            await context.SaveChangesAsync();
            tagId = tag.Id;
        }

        var service = CreateService();
        await service.DeleteTagAsync(new DeleteTagRequest { Id = tagId }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var deletedTag = await verifyContext.Tags.IgnoreQueryFilters().SingleAsync(x => x.Id == tagId);

        Assert.True(deletedTag.IsDeleted);
        Assert.NotNull(deletedTag.DeletedAtUtc);
    }

    /// <summary>
    /// Verifies that DeleteTagAsync throws NotFoundException when the tag does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteTagAsync_Should_Throw_NotFoundException_When_Tag_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteTagAsync(new DeleteTagRequest { Id = 9999 }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that DeleteTagAsync detaches all child tags (sets their ParentTagId to null) before soft-deleting the parent.
    /// </summary>
    [Fact]
    public async Task DeleteTagAsync_Should_Detach_Child_Tags()
    {
        int parentTagId;
        int childTag1Id;
        int childTag2Id;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#AABBCC" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var parent = new Tag { Name = "Sklepy", CategoryId = category.Id };
            context.Tags.Add(parent);
            await context.SaveChangesAsync();
            parentTagId = parent.Id;

            var child1 = new Tag { Name = "Biedronka", CategoryId = category.Id, ParentTagId = parentTagId };
            var child2 = new Tag { Name = "Lidl", CategoryId = category.Id, ParentTagId = parentTagId };
            context.Tags.AddRange(child1, child2);
            await context.SaveChangesAsync();
            childTag1Id = child1.Id;
            childTag2Id = child2.Id;
        }

        var service = CreateService();
        await service.DeleteTagAsync(new DeleteTagRequest { Id = parentTagId }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var detachedChild1 = await verifyContext.Tags.SingleAsync(x => x.Id == childTag1Id);
        var detachedChild2 = await verifyContext.Tags.SingleAsync(x => x.Id == childTag2Id);

        Assert.Null(detachedChild1.ParentTagId);
        Assert.Null(detachedChild2.ParentTagId);
    }
}
