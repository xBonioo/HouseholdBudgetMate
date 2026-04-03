using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IExpenseService
{
    Task<MonthPlanDto> GetMonthAsync(int year, int month, CancellationToken cancellationToken);
    Task<IReadOnlyList<AvailableMonthDto>> GetAvailableMonthsAsync(CancellationToken cancellationToken);
    Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken);
    Task<ExpenseDto> UpdateExpenseAsync(UpdateExpenseRequest request, CancellationToken cancellationToken);
    Task DeleteExpenseAsync(DeleteExpenseRequest request, CancellationToken cancellationToken);
}

