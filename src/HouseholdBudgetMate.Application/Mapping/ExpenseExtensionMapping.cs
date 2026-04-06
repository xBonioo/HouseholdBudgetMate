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
            MonthPlanId = expense.MonthPlanId,
            Order = expense.Order,
            Name = expense.Name,
            CategoryId = expense.CategoryId,
            CategoryName = expense.Category.Name,
            TagId = expense.TagId,
            TagName = expense.Tag?.Name,
            PlannedAmount = expense.PlannedAmount,
            ActualAmount = expense.LineItems.Count > 0 ? expense.LineItems.Sum(li => li.Amount) : expense.ActualAmount,
            SupportsLineItems = expense.Category.SupportsLineItems,
            ShowRemainingInUI = expense.ShowRemainingInUI,
            LineItems = expense.LineItems
                .OrderByDescending(li => li.OccurredAt)
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
            Month = item.Month
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