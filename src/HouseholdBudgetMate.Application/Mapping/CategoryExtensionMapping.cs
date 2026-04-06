using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;
using HouseholdBudgetMate.Domain.Entities;

namespace HouseholdBudgetMate.Application.Mapping;

public static class CategoryExtensionMapping
{
    public static CategoryDto MapToDto(this Category category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Color = category.Color,
            SupportsLineItems = category.SupportsLineItems,
            Tags = category.Tags
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .Select(MapTag)
                .ToList()
        };
    }

    public static TagDto MapTag(this Tag tag)
    {
        return new TagDto
        {
            Id = tag.Id,
            CategoryId = tag.CategoryId,
            Name = tag.Name
        };
    }
    
}