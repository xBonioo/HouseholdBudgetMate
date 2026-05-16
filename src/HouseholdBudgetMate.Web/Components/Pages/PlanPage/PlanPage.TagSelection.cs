using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;

namespace HouseholdBudgetMate.Web.Components.Pages.PlanPage;

public partial class PlanPage
{
    private IReadOnlyList<TagDto> GetSelectableTags(int categoryId, int? selectedTagId)
    {
        var tags = _categories.FirstOrDefault(x => x.Id == categoryId)?.Tags
            .OrderBy(x => x.Name)
            .ToList();

        if (tags == null)
        {
            return [];
        }

        if (!selectedTagId.HasValue || tags.Any(x => x.Id == selectedTagId.Value))
        {
            return tags;
        }

        var selectedTag = _categories
            .SelectMany(x => x.Tags)
            .FirstOrDefault(x => x.Id == selectedTagId.Value);

        if (selectedTag is not null)
        {
            tags.Insert(0, selectedTag);
        }

        return tags;
    }

    private IReadOnlyList<TagDto> GetSelectableRootTags(int categoryId, int? selectedTagId)
    {
        var categoryTags = GetSelectableTags(categoryId, selectedTagId);
        var descendantTagIdsByRoot = BuildDescendantTagIdsByRoot(categoryTags);

        var rootTags = categoryTags
            .Where(x => !x.ParentTagId.HasValue)
            .OrderByDescending(x => GetRootTagUsageCount(x.Id, descendantTagIdsByRoot))
            .ThenBy(x => x.Name)
            .ToList();

        if (selectedTagId.HasValue && rootTags.All(x => x.Id != selectedTagId.Value))
        {
            var selected = _categories.SelectMany(x => x.Tags).FirstOrDefault(x => x.Id == selectedTagId.Value);
            if (selected is not null)
            {
                var parent = selected.ParentTagId.HasValue
                    ? _categories.SelectMany(x => x.Tags).FirstOrDefault(x => x.Id == selected.ParentTagId.Value)
                    : selected;

                if (parent is not null && rootTags.All(x => x.Id != parent.Id))
                {
                    rootTags.Insert(0, parent);
                }
            }
        }

        return rootTags;
    }

    private IReadOnlyList<TagDto> GetSelectableSubTags(int categoryId, int? rootTagId, int? selectedTagId)
    {
        if (!rootTagId.HasValue)
        {
            return [];
        }

        var subTags = GetSelectableTags(categoryId, selectedTagId)
            .Where(x => x.ParentTagId == rootTagId.Value)
            .OrderByDescending(x => GetTagUsageCount(x.Id))
            .ThenBy(x => x.Name)
            .ToList();

        if (selectedTagId.HasValue && subTags.All(x => x.Id != selectedTagId.Value))
        {
            var selected = _categories.SelectMany(x => x.Tags).FirstOrDefault(x => x.Id == selectedTagId.Value);
            if (selected is not null && selected.ParentTagId == rootTagId.Value)
            {
                subTags.Insert(0, selected);
            }
        }

        return subTags;
    }

    private bool HasSubTags(int categoryId, int? rootTagId)
    {
        if (!rootTagId.HasValue)
        {
            return false;
        }

        return _categories
            .FirstOrDefault(x => x.Id == categoryId)
            ?.Tags
            .Any(x => x.ParentTagId == rootTagId.Value) == true;
    }

    private bool CanSelectSubTag(int categoryId, int? rootTagId)
    {
        return rootTagId.HasValue
               && !SupportsLineItemsForSelection(categoryId, rootTagId)
               && HasSubTags(categoryId, rootTagId);
    }

    private int? GetRootTagId(int? tagId)
    {
        if (!tagId.HasValue)
        {
            return null;
        }

        var tag = _categories.SelectMany(x => x.Tags).FirstOrDefault(x => x.Id == tagId.Value);
        return tag?.ParentTagId ?? tagId;
    }

    private int? GetSelectedSubTagIdForCreateExpense()
    {
        if (!CanSelectSubTag(_newExpense.CategoryId, _newExpenseRootTagId)
            || !_newExpenseRootTagId.HasValue
            || !_newExpense.TagId.HasValue)
        {
            return null;
        }

        var selectedTag = _categories.SelectMany(x => x.Tags).FirstOrDefault(x => x.Id == _newExpense.TagId.Value);
        return selectedTag?.ParentTagId == _newExpenseRootTagId ? selectedTag.Id : null;
    }

    private int? GetSelectedSubTagIdForEditExpense()
    {
        if (_editExpense is null
            || !CanSelectSubTag(_editExpense.CategoryId, _editExpenseRootTagId)
            || !_editExpenseRootTagId.HasValue
            || !_editExpense.TagId.HasValue)
        {
            return null;
        }

        var selectedTag = _categories.SelectMany(x => x.Tags).FirstOrDefault(x => x.Id == _editExpense.TagId.Value);
        return selectedTag?.ParentTagId == _editExpenseRootTagId ? selectedTag.Id : null;
    }

    private Task OnCreateCategoryChanged(int categoryId)
    {
        _newExpense.CategoryId = categoryId;
        _newExpenseRootTagId = null;
        _newExpense.TagId = null;

        if (SupportsLineItemsForSelection(categoryId, _newExpense.TagId))
        {
            _newExpense.ActualAmount = 0;
            _newExpenseActualAmountInput = FormatDecimalInput(_newExpense.ActualAmount);
        }

        return Task.CompletedTask;
    }

    private Task OnEditCategoryChanged(int categoryId)
    {
        if (_editExpense is null)
        {
            return Task.CompletedTask;
        }

        _editExpense.CategoryId = categoryId;
        _editExpenseRootTagId = null;
        _editExpense.TagId = null;

        if (SupportsLineItemsForSelection(_editExpense.CategoryId, _editExpense.TagId))
        {
            _editExpense.ActualAmount = 0;
            _editExpenseActualAmountInput = FormatDecimalInput(_editExpense.ActualAmount);
        }

        return Task.CompletedTask;
    }

    private Task OnCreateRootTagChanged(int? rootTagId)
    {
        _newExpenseRootTagId = rootTagId;
        _newExpense.TagId = rootTagId;

        if (SupportsLineItemsForSelection(_newExpense.CategoryId, _newExpense.TagId))
        {
            _newExpense.ActualAmount = 0;
            _newExpenseActualAmountInput = FormatDecimalInput(_newExpense.ActualAmount);
        }

        return Task.CompletedTask;
    }

    private Task OnCreateSubTagChanged(int? subTagId)
    {
        if (SupportsLineItemsForSelection(_newExpense.CategoryId, _newExpenseRootTagId))
        {
            _newExpense.TagId = _newExpenseRootTagId;
            return Task.CompletedTask;
        }

        _newExpense.TagId = subTagId ?? _newExpenseRootTagId;

        if (SupportsLineItemsForSelection(_newExpense.CategoryId, _newExpense.TagId))
        {
            _newExpense.ActualAmount = 0;
            _newExpenseActualAmountInput = FormatDecimalInput(_newExpense.ActualAmount);
        }

        return Task.CompletedTask;
    }

    private Task OnEditRootTagChanged(int? rootTagId)
    {
        if (_editExpense is null)
        {
            return Task.CompletedTask;
        }

        _editExpenseRootTagId = rootTagId;
        _editExpense.TagId = rootTagId;

        if (SupportsLineItemsForSelection(_editExpense.CategoryId, _editExpense.TagId))
        {
            _editExpense.ActualAmount = 0;
            _editExpenseActualAmountInput = FormatDecimalInput(_editExpense.ActualAmount);
        }

        return Task.CompletedTask;
    }

    private Task OnEditSubTagChanged(int? subTagId)
    {
        if (_editExpense is null)
        {
            return Task.CompletedTask;
        }

        if (SupportsLineItemsForSelection(_editExpense.CategoryId, _editExpenseRootTagId))
        {
            _editExpense.TagId = _editExpenseRootTagId;
            return Task.CompletedTask;
        }

        _editExpense.TagId = subTagId ?? _editExpenseRootTagId;

        if (SupportsLineItemsForSelection(_editExpense.CategoryId, _editExpense.TagId))
        {
            _editExpense.ActualAmount = 0;
            _editExpenseActualAmountInput = FormatDecimalInput(_editExpense.ActualAmount);
        }

        return Task.CompletedTask;
    }

    private IReadOnlyList<TagDto> GetSelectableLineItemTags(int categoryId, int? expenseMainTagId, int? selectedTagId)
    {
        var allTags = GetSelectableTags(categoryId, selectedTagId);
        if (!expenseMainTagId.HasValue)
        {
            return allTags;
        }

        var scopedTags = allTags
            .Where(x => x.ParentTagId == expenseMainTagId.Value)
            .OrderByDescending(x => GetTagUsageCount(x.Id))
            .ThenBy(x => x.Name)
            .ToList();

        if (selectedTagId.HasValue && scopedTags.All(x => x.Id != selectedTagId.Value))
        {
            var selected = allTags.FirstOrDefault(x => x.Id == selectedTagId.Value);
            if (selected is not null && selected.ParentTagId == expenseMainTagId.Value)
            {
                scopedTags.Add(selected);
            }
        }

        return scopedTags;
    }

    private int GetTagUsageCount(int tagId)
    {
        return _tagUsageCountByTagId.GetValueOrDefault(tagId, 0);
    }

    private int GetRootTagUsageCount(int rootTagId, IReadOnlyDictionary<int, IReadOnlyCollection<int>> descendantTagIdsByRoot)
    {
        var usageCount = GetTagUsageCount(rootTagId);

        if (!descendantTagIdsByRoot.TryGetValue(rootTagId, out var descendants))
        {
            return usageCount;
        }

        foreach (var descendantId in descendants)
        {
            usageCount += GetTagUsageCount(descendantId);
        }

        return usageCount;
    }

    private static IReadOnlyDictionary<int, IReadOnlyCollection<int>> BuildDescendantTagIdsByRoot(IReadOnlyList<TagDto> tags)
    {
        var tagsByParentId = tags
            .Where(x => x.ParentTagId.HasValue)
            .GroupBy(x => x.ParentTagId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(v => v.Id).ToList());

        var rootTagIds = tags
            .Where(x => !x.ParentTagId.HasValue)
            .Select(x => x.Id)
            .ToList();

        var descendantsByRoot = new Dictionary<int, IReadOnlyCollection<int>>();

        foreach (var rootTagId in rootTagIds)
        {
            var descendants = new List<int>();
            var pending = new Stack<int>();

            if (tagsByParentId.TryGetValue(rootTagId, out var directChildren))
            {
                foreach (var childId in directChildren)
                {
                    pending.Push(childId);
                }
            }

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                descendants.Add(current);

                if (!tagsByParentId.TryGetValue(current, out var children))
                {
                    continue;
                }

                foreach (var childId in children)
                {
                    pending.Push(childId);
                }
            }

            descendantsByRoot[rootTagId] = descendants;
        }

        return descendantsByRoot;
    }
}

