using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Application.Mapping;
using HouseholdBudgetMate.Application.Validation;
using HouseholdBudgetMate.Application.Validation.Categories;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class CategoryService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider) : ICategoryService
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

    public async Task DeleteCategoryAsync(DeleteCategoryRequest request, CancellationToken cancellationToken)
    {
        DeleteCategoryValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var category = await dbContext.Categories
                           .Include(x => x.Tags)
                           .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException("Category not found.");

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

        var categoryExists = await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new NotFoundException("Category not found.");
        }

        await EnsureTagNameUniqueAsync(dbContext, request.CategoryId, normalizedName, null, cancellationToken);
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

        var tag = await dbContext.Tags
                      .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found.");

        var categoryExists = await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new NotFoundException("Category not found.");
        }

        await EnsureTagNameUniqueAsync(dbContext, request.CategoryId, normalizedName, tag.Id, cancellationToken);
        await EnsureParentTagValidAsync(dbContext, request.CategoryId, request.ParentTagId, tag.Id, cancellationToken);

        tag.CategoryId = request.CategoryId;
        tag.Name = request.Name;
        tag.ParentTagId = request.ParentTagId;
        tag.SupportsLineItemsOverride = request.SupportsLineItemsOverride;

        await dbContext.SaveChangesAsync(cancellationToken);

        return tag.MapTag();
    }

    public async Task DeleteTagAsync(DeleteTagRequest request, CancellationToken cancellationToken)
    {
        DeleteTagValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tag = await dbContext.Tags
                      .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found.");

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
        string normalizedName,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Tags
            .IgnoreQueryFilters()
            .AnyAsync(x => x.CategoryId == categoryId
                           && !x.IsDeleted
                           && (!excludeId.HasValue || x.Id != excludeId.Value)
                           && x.Name.ToUpper() == normalizedName,
                cancellationToken);

        if (exists)
        {
            throw new ConflictException("Tag name must be unique within category.");
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
}
