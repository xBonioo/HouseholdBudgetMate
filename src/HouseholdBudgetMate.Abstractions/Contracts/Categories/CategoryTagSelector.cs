using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;

namespace HouseholdBudgetMate.Abstractions.Contracts.Categories;

public static class CategoryTagSelector
{
    public static IReadOnlyList<TagDto> GetSelectableTags(
        IEnumerable<CategoryDto> categories,
        int categoryId,
        int? selectedTagId)
    {
        var categoryList = categories as IReadOnlyList<CategoryDto> ?? categories.ToList();
        var tags = categoryList
            .FirstOrDefault(x => x.Id == categoryId)?
            .Tags
            .OrderBy(x => x.Name)
            .ToList();

        if (tags is null)
        {
            return [];
        }

        if (!selectedTagId.HasValue || tags.Any(x => x.Id == selectedTagId.Value))
        {
            return tags;
        }

        var selectedTag = categoryList
            .SelectMany(x => x.Tags)
            .FirstOrDefault(x => x.Id == selectedTagId.Value);

        if (selectedTag is not null)
        {
            tags.Insert(0, selectedTag);
        }

        return tags;
    }

    public static IReadOnlyList<TagDto> GetSelectableRootTags(
        IEnumerable<CategoryDto> categories,
        int categoryId,
        int? selectedTagId)
    {
        return GetSelectableTags(categories, categoryId, selectedTagId)
            .Where(x => !x.ParentTagId.HasValue)
            .ToList();
    }
}
