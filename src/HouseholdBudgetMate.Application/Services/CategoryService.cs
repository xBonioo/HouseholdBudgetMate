using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class CategoryService(IDbContextFactory<ApplicationDbContext> dbContextFactory) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Categories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new CategoryDto
            {
                Id = x.Id,
                Name = x.Name,
                Color = x.Color,
                Tags = x.Tags
                    .OrderBy(t => t.Name)
                    .Select(t => new TagDto
                    {
                        Id = t.Id,
                        CategoryId = t.CategoryId,
                        Name = t.Name
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(request.Name);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await EnsureCategoryNameUniqueAsync(dbContext, normalizedName, null, cancellationToken);

        var category = new Category
        {
            Name = request.Name.Trim(),
            Color = request.Color.Trim()
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(request.Name);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var category = await dbContext.Categories
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Category not found.");

        await EnsureCategoryNameUniqueAsync(dbContext, normalizedName, category.Id, cancellationToken);

        category.Name = request.Name.Trim();
        category.Color = request.Color.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task DeleteCategoryAsync(DeleteCategoryRequest request, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var category = await dbContext.Categories
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Category not found.");

        category.IsDeleted = true;
        category.DeletedAtUtc = DateTime.UtcNow;

        foreach (var tag in category.Tags.Where(x => !x.IsDeleted))
        {
            tag.IsDeleted = true;
            tag.DeletedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TagDto> CreateTagAsync(CreateTagRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(request.Name);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var categoryExists = await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new NotFoundException("Category not found.");
        }

        await EnsureTagNameUniqueAsync(dbContext, request.CategoryId, normalizedName, null, cancellationToken);

        var tag = new Tag
        {
            CategoryId = request.CategoryId,
            Name = request.Name.Trim()
        };

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapTag(tag);
    }

    public async Task<TagDto> UpdateTagAsync(UpdateTagRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(request.Name);

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

        tag.CategoryId = request.CategoryId;
        tag.Name = request.Name.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapTag(tag);
    }

    public async Task DeleteTagAsync(DeleteTagRequest request, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tag = await dbContext.Tags
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Tag not found.");

        tag.IsDeleted = true;
        tag.DeletedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CategoryDto MapCategory(Category category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Color = category.Color,
            Tags = category.Tags
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .Select(MapTag)
                .ToList()
        };
    }

    private static TagDto MapTag(Tag tag)
    {
        return new TagDto
        {
            Id = tag.Id,
            CategoryId = tag.CategoryId,
            Name = tag.Name
        };
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException("Name is required.");
        }

        return value.Trim().ToUpperInvariant();
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
}

