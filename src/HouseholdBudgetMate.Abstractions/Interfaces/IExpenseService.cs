using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IExpenseService
{
    Task<MonthPlanDto> GetMonthAsync(int year, int month, CancellationToken cancellationToken);
    Task<IReadOnlyList<AvailableMonthDto>> GetAvailableMonthsAsync(CancellationToken cancellationToken);

    Task<MonthSavingsTransferItemDto> CreateMonthSavingsTransferItemAsync(CreateMonthSavingsTransferItemRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken);

    Task<ExpenseLineItemDto> CreateExpenseLineItemAsync(CreateExpenseLineItemRequest request,
        CancellationToken cancellationToken);

    Task<MonthSavingsTransferItemDto> UpdateMonthSavingsTransferItemAsync(UpdateMonthSavingsTransferItemRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseDto> UpdateExpenseAsync(UpdateExpenseRequest request, CancellationToken cancellationToken);

    Task ReorderExpensesAsync(ReorderExpensesRequest request, CancellationToken cancellationToken);

    Task<ExpenseLineItemDto> UpdateExpenseLineItemAsync(UpdateExpenseLineItemRequest request,
        CancellationToken cancellationToken);

    Task DeleteExpenseAsync(DeleteExpenseRequest request, CancellationToken cancellationToken);
    Task DeleteExpenseLineItemAsync(DeleteExpenseLineItemRequest request, CancellationToken cancellationToken);

    Task DeleteMonthSavingsTransferItemAsync(DeleteMonthSavingsTransferItemRequest request,
        CancellationToken cancellationToken);
}