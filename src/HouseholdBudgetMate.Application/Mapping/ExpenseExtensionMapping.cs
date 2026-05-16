using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Domain.Entities;

namespace HouseholdBudgetMate.Application.Mapping;

public static class ExpenseExtensionMapping
{
    public static ExpenseDto MapExpenseToDto(this Expense expense)
    {
        return new ExpenseDto
        {
            Id = expense.Id,
            UpdatedAtUtc = expense.UpdatedAtUtc,
            MonthPlanId = expense.MonthPlanId,
            Order = expense.Order,
            Name = expense.Name,
            CategoryId = expense.CategoryId,
            CategoryName = expense.Category.Name,
            RegularExpenseDefinitionId = expense.RegularExpenseDefinitionId,
            TagId = expense.TagId,
            TagName = expense.Tag?.Name,
            PlannedAmount = expense.PlannedAmount,
            ActualAmount = expense.LineItems.Count > 0 ? expense.LineItems.Sum(li => li.Amount) : expense.ActualAmount,
            SupportsLineItems = expense.Tag?.SupportsLineItemsOverride ?? expense.Category.SupportsLineItems,
            ShowRemainingInUI = expense.ShowRemainingInUI,
            LineItems = expense.LineItems
                .OrderByDescending(li => li.OccurredAt.DayNumber)
                .ThenBy(li => li.Id)
                .Select(MapLineItemToDto)
                .ToList()
        };
    }

    public static MonthSavingsTransferItemDto MapSavingsTransferToDto(this MonthSavingsTransferItem item)
    {
        return new MonthSavingsTransferItemDto
        {
            Id = item.Id,
            MonthPlanId = item.MonthPlanId,
            Amount = item.Amount,
            TransferDate = item.TransferDate
        };
    }

    public static AvailableMonthDto MapAvailableMonthToDto(this MonthPlan item)
    {
        return new AvailableMonthDto
        {
            Year = item.Year,
            Month = item.Month,
            IsClosed = item.IsClosed
        };
    }

    public static RegularExpenseDefinitionDto MapRegularExpenseDefinitionToDto(this RegularExpenseDefinition definition)
    {
        return new RegularExpenseDefinitionDto
        {
            Id = definition.Id,
            Order = definition.Order,
            Name = definition.Name,
            CategoryId = definition.CategoryId,
            CategoryName = definition.Category.Name,
            TagId = definition.TagId,
            TagName = definition.Tag?.Name,
            Amount = definition.Amount,
            IsActive = definition.IsActive,
            ShowRemainingInUI = definition.ShowRemainingInUI
        };
    }

    private static ExpenseLineItemDto MapLineItemToDto(ExpenseLineItem lineItem)
    {
        return new ExpenseLineItemDto
        {
            Id = lineItem.Id,
            ExpenseId = lineItem.ExpenseId,
            Description = lineItem.Description,
            Amount = lineItem.Amount,
            OccurredAt = lineItem.OccurredAt,
            TagId = lineItem.TagId,
            TagName = lineItem.Tag?.Name
        };
    }
}