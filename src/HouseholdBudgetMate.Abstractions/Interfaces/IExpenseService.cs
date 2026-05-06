using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IExpenseService
{
    Task<MonthPlanDto> GetMonthAsync(int year, int month, CancellationToken cancellationToken);
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(int year, int month, CancellationToken cancellationToken);
    Task<YearStatisticsDto> GetYearStatisticsAsync(int year, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExpenseHistorySearchResultDto>> SearchExpenseHistoryAsync(
        SearchExpenseHistoryRequest request,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryLifetimeExpenseTotalDto>> GetCategoryLifetimeExpenseTotalsAsync(
        IReadOnlyList<int>? categoryIds,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AvailableMonthDto>> GetAvailableMonthsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RegularExpenseDefinitionDto>> GetRegularExpenseDefinitionsAsync(CancellationToken cancellationToken);

    Task<MonthSavingsTransferItemDto> CreateMonthSavingsTransferItemAsync(CreateMonthSavingsTransferItemRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken);
    Task<RegularExpenseDefinitionDto> CreateRegularExpenseDefinitionAsync(CreateRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseLineItemDto> CreateExpenseLineItemAsync(CreateExpenseLineItemRequest request,
        CancellationToken cancellationToken);

    Task<MonthSavingsTransferItemDto> UpdateMonthSavingsTransferItemAsync(UpdateMonthSavingsTransferItemRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseDto> UpdateExpenseAsync(UpdateExpenseRequest request, CancellationToken cancellationToken);
    Task<RegularExpenseDefinitionDto> UpdateRegularExpenseDefinitionAsync(UpdateRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken);

    Task ReorderExpensesAsync(ReorderExpensesRequest request, CancellationToken cancellationToken);
    Task<int> CopySelectedExpensesToNextMonthAsync(CopySelectedExpensesToNextMonthRequest request,
        CancellationToken cancellationToken);
    Task ReorderRegularExpenseDefinitionsAsync(ReorderRegularExpenseDefinitionsRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseLineItemDto> UpdateExpenseLineItemAsync(UpdateExpenseLineItemRequest request,
        CancellationToken cancellationToken);

    Task DeleteExpenseAsync(DeleteExpenseRequest request, CancellationToken cancellationToken);
    Task DeleteRegularExpenseDefinitionAsync(DeleteRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken);
    Task DeleteRegularExpenseDefinitionPermanentlyAsync(DeleteRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken);
    Task DeleteExpenseLineItemAsync(DeleteExpenseLineItemRequest request, CancellationToken cancellationToken);

    Task DeleteMonthSavingsTransferItemAsync(DeleteMonthSavingsTransferItemRequest request,
        CancellationToken cancellationToken);

    Task CloseMonthAsync(int year, int month, CancellationToken cancellationToken);
    Task OpenMonthAsync(int year, int month, CancellationToken cancellationToken);
    Task<bool> AddRegularExpenseDefinitionToMonthAsync(int definitionId, int year, int month, CancellationToken cancellationToken);
}