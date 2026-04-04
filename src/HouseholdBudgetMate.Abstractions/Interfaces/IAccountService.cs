using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken);
    Task<AccountDto> UpdateAccountAsync(UpdateAccountRequest request, CancellationToken cancellationToken);
    Task DeleteAccountAsync(DeleteAccountRequest request, CancellationToken cancellationToken);
    Task SetAccountArchivedAsync(SetAccountArchivedRequest request, CancellationToken cancellationToken);
    Task ReorderAccountsAsync(ReorderAccountsRequest request, CancellationToken cancellationToken);
    Task<AccountMonthBalanceDto> UpsertMonthBalanceAsync(UpsertAccountMonthBalanceRequest request, CancellationToken cancellationToken);
    Task<AccountMonthBalanceDto> UpdateMonthBalanceAmountAsync(UpdateAccountMonthBalanceAmountRequest request, CancellationToken cancellationToken);
    Task DeleteMonthBalanceAsync(DeleteAccountMonthBalanceRequest request, CancellationToken cancellationToken);
}

