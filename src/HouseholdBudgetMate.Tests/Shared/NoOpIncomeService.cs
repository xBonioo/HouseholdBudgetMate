using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;

namespace HouseholdBudgetMate.Tests.Shared;

public sealed class NoOpIncomeService : IIncomeService
{
    public Task<IReadOnlyList<IncomeDto>> GetMonthIncomesAsync(int year, int month, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<IncomeDto>>([]);

    public Task<LiveBalanceDto> GetLiveBalanceAsync(int year, int month, CancellationToken cancellationToken)
        => Task.FromResult(new LiveBalanceDto());

    public Task<IReadOnlyList<RegularIncomeDefinitionDto>> GetRegularDefinitionsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RegularIncomeDefinitionDto>>([]);

    public Task<IncomeDto> CreateIncomeAsync(CreateIncomeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new IncomeDto());

    public Task<RegularIncomeDefinitionDto> CreateRegularDefinitionAsync(CreateRegularIncomeDefinitionRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(new RegularIncomeDefinitionDto());

    public Task<IncomeDto> UpdateIncomeAsync(UpdateIncomeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new IncomeDto());

    public Task<RegularIncomeDefinitionDto> UpdateRegularDefinitionAsync(UpdateRegularIncomeDefinitionRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(new RegularIncomeDefinitionDto());

    public Task DeleteIncomeAsync(DeleteIncomeRequest request, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task DeleteRegularDefinitionAsync(DeleteRegularIncomeDefinitionRequest request,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task DeleteRegularDefinitionPermanentlyAsync(DeleteRegularIncomeDefinitionRequest request,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task SyncRegularIncomesForMonthAsync(int year, int month, CancellationToken cancellationToken)
        => Task.CompletedTask;
}