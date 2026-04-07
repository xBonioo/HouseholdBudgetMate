using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IIncomeService
{
    Task<IReadOnlyList<IncomeDto>> GetMonthIncomesAsync(int year, int month, CancellationToken cancellationToken);
    Task<LiveBalanceDto> GetLiveBalanceAsync(int year, int month, CancellationToken cancellationToken);
    Task<IReadOnlyList<RegularIncomeDefinitionDto>> GetRegularDefinitionsAsync(CancellationToken cancellationToken);
 
    Task<IncomeDto> CreateIncomeAsync(CreateIncomeRequest request, CancellationToken cancellationToken);

    Task<RegularIncomeDefinitionDto> CreateRegularDefinitionAsync(CreateRegularIncomeDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<IncomeDto> UpdateIncomeAsync(UpdateIncomeRequest request, CancellationToken cancellationToken);

    Task<RegularIncomeDefinitionDto> UpdateRegularDefinitionAsync(UpdateRegularIncomeDefinitionRequest request,
        CancellationToken cancellationToken);

    Task DeleteIncomeAsync(DeleteIncomeRequest request, CancellationToken cancellationToken);

    Task DeleteRegularDefinitionAsync(DeleteRegularIncomeDefinitionRequest request,
        CancellationToken cancellationToken);

    Task DeleteRegularDefinitionPermanentlyAsync(DeleteRegularIncomeDefinitionRequest request,
        CancellationToken cancellationToken);

    Task SyncRegularIncomesForMonthAsync(int year, int month, CancellationToken cancellationToken);
}