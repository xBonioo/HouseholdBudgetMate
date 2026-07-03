using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Application.Mapping;
using HouseholdBudgetMate.Application.Validation;
using HouseholdBudgetMate.Application.Validation.Categories;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class CategoryService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider,
    CurrentUserContext currentUserContext) : ICategoryService
{
    private static readonly CreateCategoryRequestValidator CreateCategoryValidator = new();
    private static readonly CreateTagRequestValidator CreateTagValidator = new();
    private static readonly UpdateCategoryRequestValidator UpdateCategoryValidator = new();
    private static readonly UpdateTagRequestValidator UpdateTagValidator = new();
    private static readonly DeleteCategoryRequestValidator DeleteCategoryValidator = new();
    private static readonly DeleteTagRequestValidator DeleteTagValidator = new();

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var categories = await dbContext.Categories
            .Include(x => x.Tags)
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.MapToDto())
            .ToListAsync(cancellationToken);

        return categories;
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        CreateCategoryValidator.ValidateOrThrowBadRequest(request);
        var normalizedName = request.Name.ToUpperInvariant();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);

        await EnsureCategoryNameUniqueAsync(dbContext, normalizedName, null, cancellationToken);

        var category = new Category
        {
            Name = request.Name,
            Color = request.Color,
            EnvelopeLimit = request.EnvelopeLimit,
            SupportsLineItems = request.SupportsLineItems
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return category.MapToDto();
    }

    public async Task<CategoryDto> UpdateCategoryAsync(UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        UpdateCategoryValidator.ValidateOrThrowBadRequest(request);
        var normalizedName = request.Name.ToUpperInvariant();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);

        var category = await dbContext.Categories
                           .Include(x => x.Tags)
                           .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException("Category not found.");

        await EnsureCategoryNameUniqueAsync(dbContext, normalizedName, category.Id, cancellationToken);

        category.Name = request.Name;
        category.Color = request.Color;
        category.EnvelopeLimit = request.EnvelopeLimit;
        category.SupportsLineItems = request.SupportsLineItems;

        await dbContext.SaveChangesAsync(cancellationToken);

        return category.MapToDto();
    }

    public async Task<CategoryDeletionImpactDto> GetCategoryDeletionImpactAsync(
        int categoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId <= 0)
        {
            throw new BadRequestException("Category id is required.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);

        var category = await dbContext.Categories
                           .AsNoTracking()
                           .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken)
                       ?? throw new NotFoundException("Category not found.");

        var tagIds = await dbContext.Tags
            .Where(x => x.CategoryId == categoryId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return new CategoryDeletionImpactDto
        {
            CategoryId = category.Id,
            CategoryName = category.Name,
            ExpenseCount = await dbContext.Expenses.CountAsync(x => x.CategoryId == categoryId, cancellationToken),
            ExpenseLineItemCount = tagIds.Count == 0
                ? 0
                : await dbContext.ExpenseLineItems.CountAsync(
                    x => x.TagId.HasValue && tagIds.Contains(x.TagId.Value),
                    cancellationToken)
        };
    }

    public async Task DeleteCategoryAsync(DeleteCategoryRequest request, CancellationToken cancellationToken)
    {
        DeleteCategoryValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);

        var category = await dbContext.Categories
                           .Include(x => x.Tags)
                           .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException("Category not found.");

        await ReassignCategoryAssignmentsAsync(dbContext, category.Id, request, cancellationToken);

        category.IsDeleted = true;
        category.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();

        foreach (var tag in category.Tags.Where(x => !x.IsDeleted))
        {
            tag.IsDeleted = true;
            tag.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TagDto> CreateTagAsync(CreateTagRequest request, CancellationToken cancellationToken)
    {
        CreateTagValidator.ValidateOrThrowBadRequest(request);
        var normalizedName = request.Name.ToUpperInvariant();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);

        var categoryExists = await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new NotFoundException("Category not found.");
        }

        await EnsureTagNameUniqueAsync(
            dbContext,
            request.CategoryId,
            request.ParentTagId,
            normalizedName,
            null,
            cancellationToken);
        await EnsureParentTagValidAsync(dbContext, request.CategoryId, request.ParentTagId, null, cancellationToken);

        var tag = new Tag
        {
            CategoryId = request.CategoryId,
            Name = request.Name,
            ParentTagId = request.ParentTagId,
            SupportsLineItemsOverride = request.SupportsLineItemsOverride
        };

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tag.MapTag();
    }

    public async Task<TagDto> UpdateTagAsync(UpdateTagRequest request, CancellationToken cancellationToken)
    {
        UpdateTagValidator.ValidateOrThrowBadRequest(request);
        var normalizedName = request.Name.ToUpperInvariant();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);

        var tag = await dbContext.Tags
                      .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found.");

        var categoryExists = await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new NotFoundException("Category not found.");
        }

        await EnsureTagNameUniqueAsync(
            dbContext,
            request.CategoryId,
            request.ParentTagId,
            normalizedName,
            tag.Id,
            cancellationToken);
        await EnsureParentTagValidAsync(dbContext, request.CategoryId, request.ParentTagId, tag.Id, cancellationToken);

        tag.CategoryId = request.CategoryId;
        tag.Name = request.Name;
        tag.ParentTagId = request.ParentTagId;
        tag.SupportsLineItemsOverride = request.SupportsLineItemsOverride;

        await dbContext.SaveChangesAsync(cancellationToken);

        return tag.MapTag();
    }

    public async Task<TagDeletionImpactDto> GetTagDeletionImpactAsync(int tagId, CancellationToken cancellationToken)
    {
        if (tagId <= 0)
        {
            throw new BadRequestException("Tag id is required.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);

        var tag = await dbContext.Tags
                      .AsNoTracking()
                      .FirstOrDefaultAsync(x => x.Id == tagId, cancellationToken)
                  ?? throw new NotFoundException("Tag not found.");

        return new TagDeletionImpactDto
        {
            TagId = tag.Id,
            TagName = tag.Name,
            CategoryId = tag.CategoryId,
            ExpenseCount = await dbContext.Expenses.CountAsync(x => x.TagId == tagId, cancellationToken),
            ExpenseLineItemCount = await dbContext.ExpenseLineItems.CountAsync(x => x.TagId == tagId, cancellationToken)
        };
    }

    public async Task DeleteTagAsync(DeleteTagRequest request, CancellationToken cancellationToken)
    {
        DeleteTagValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);

        var tag = await dbContext.Tags
                      .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found.");

        await ReassignTagAssignmentsAsync(dbContext, tag, request, cancellationToken);

        var childTags = await dbContext.Tags
            .Where(x => x.ParentTagId == tag.Id)
            .ToListAsync(cancellationToken);

        foreach (var childTag in childTags)
        {
            childTag.ParentTagId = null;
        }

        tag.IsDeleted = true;
        tag.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task ReassignCategoryAssignmentsAsync(
        ApplicationDbContext dbContext,
        int categoryId,
        DeleteCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var expenses = await dbContext.Expenses
            .Where(x => x.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        var categoryTagIds = await dbContext.Tags
            .Where(x => x.CategoryId == categoryId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var lineItems = categoryTagIds.Count == 0
            ? []
            : await dbContext.ExpenseLineItems
                .Where(x => x.TagId.HasValue && categoryTagIds.Contains(x.TagId.Value))
                .ToListAsync(cancellationToken);

        if (expenses.Count == 0 && lineItems.Count == 0)
        {
            return;
        }

        if (!request.ReplacementCategoryId.HasValue)
        {
            throw new ConflictException("Category is used by expenses. Choose a replacement category before deleting it.");
        }

        if (request.ReplacementCategoryId.Value == categoryId)
        {
            throw new BadRequestException("Replacement category must be different from deleted category.");
        }

        var replacementCategoryExists = await dbContext.Categories
            .AnyAsync(x => x.Id == request.ReplacementCategoryId.Value, cancellationToken);
        if (!replacementCategoryExists)
        {
            throw new NotFoundException("Replacement category not found.");
        }

        if (request.ReplacementTagId.HasValue)
        {
            await EnsureTagBelongsToCategoryAsync(
                dbContext,
                request.ReplacementTagId.Value,
                request.ReplacementCategoryId.Value,
                cancellationToken);
        }

        foreach (var expense in expenses)
        {
            expense.CategoryId = request.ReplacementCategoryId.Value;
            expense.TagId = request.ReplacementTagId;
        }

        foreach (var lineItem in lineItems)
        {
            lineItem.TagId = request.ReplacementTagId;
        }
    }

    private static async Task ReassignTagAssignmentsAsync(
        ApplicationDbContext dbContext,
        Tag tag,
        DeleteTagRequest request,
        CancellationToken cancellationToken)
    {
        var expenses = await dbContext.Expenses
            .Where(x => x.TagId == tag.Id)
            .ToListAsync(cancellationToken);

        var lineItems = await dbContext.ExpenseLineItems
            .Where(x => x.TagId == tag.Id)
            .ToListAsync(cancellationToken);

        if (expenses.Count == 0 && lineItems.Count == 0)
        {
            return;
        }

        if (!request.ClearAssignments && !request.ReplacementTagId.HasValue)
        {
            throw new ConflictException("Tag is used by expenses. Choose a replacement tag or clear the tag before deleting it.");
        }

        if (request.ReplacementTagId == tag.Id)
        {
            throw new BadRequestException("Replacement tag must be different from deleted tag.");
        }

        if (request.ReplacementTagId.HasValue)
        {
            await EnsureTagBelongsToCategoryAsync(
                dbContext,
                request.ReplacementTagId.Value,
                tag.CategoryId,
                cancellationToken);
        }

        foreach (var expense in expenses)
        {
            expense.TagId = request.ReplacementTagId;
        }

        foreach (var lineItem in lineItems)
        {
            lineItem.TagId = request.ReplacementTagId;
        }
    }

    private static async Task EnsureCategoryNameUniqueAsync(
        ApplicationDbContext dbContext,
        string normalizedName,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Categories
            .IgnoreQueryFilters()
            .AnyAsync(x => !x.IsDeleted
                           && (!excludeId.HasValue || x.Id != excludeId.Value)
                           && x.Name.ToUpper() == normalizedName,
                cancellationToken);

        if (exists)
        {
            throw new ConflictException("Category name must be unique.");
        }
    }

    private static async Task EnsureTagNameUniqueAsync(
        ApplicationDbContext dbContext,
        int categoryId,
        int? parentTagId,
        string normalizedName,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Tags
            .IgnoreQueryFilters()
            .AnyAsync(x => x.CategoryId == categoryId
                           && x.ParentTagId == parentTagId
                           && !x.IsDeleted
                           && (!excludeId.HasValue || x.Id != excludeId.Value)
                           && x.Name.ToUpper() == normalizedName,
                cancellationToken);

        if (exists)
        {
            throw new ConflictException("Tag name must be unique within the same parent tag.");
        }
    }

    private static async Task EnsureTagBelongsToCategoryAsync(
        ApplicationDbContext dbContext,
        int tagId,
        int categoryId,
        CancellationToken cancellationToken)
    {
        var belongs = await dbContext.Tags
            .AnyAsync(x => x.Id == tagId && x.CategoryId == categoryId, cancellationToken);
        if (!belongs)
        {
            throw new BadRequestException("Replacement tag must belong to selected category.");
        }
    }

    private static async Task EnsureParentTagValidAsync(
        ApplicationDbContext dbContext,
        int categoryId,
        int? parentTagId,
        int? currentTagId,
        CancellationToken cancellationToken)
    {
        if (!parentTagId.HasValue)
        {
            return;
        }

        if (currentTagId.HasValue && currentTagId.Value == parentTagId.Value)
        {
            throw new BadRequestException("Tag cannot be its own parent.");
        }

        var parentTag = await dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == parentTagId.Value, cancellationToken)
            ?? throw new NotFoundException("Parent tag not found.");

        if (parentTag.CategoryId != categoryId)
        {
            throw new BadRequestException("Parent tag must belong to selected category.");
        }

        if (parentTag.ParentTagId.HasValue)
        {
            throw new BadRequestException("Only one level of tag hierarchy is supported.");
        }
    }

    private async Task EnsureCurrentUserIsAdminAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserContext.UserId))
        {
            throw new ForbiddenException("Admin permissions are required.");
        }

        var isAdmin = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == currentUserContext.UserId
                     && x.Id != User.DefaultUserId
                     && x.IsAdmin,
                cancellationToken);

        if (!isAdmin)
        {
            throw new ForbiddenException("Admin permissions are required.");
        }
    }
}
