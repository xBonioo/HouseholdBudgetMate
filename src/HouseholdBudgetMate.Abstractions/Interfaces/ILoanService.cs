using HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface ILoanService
{
    Task<IReadOnlyList<LoanDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<LoanDto> CreateLoanAsync(CreateLoanRequest request, CancellationToken cancellationToken);
    Task<LoanDto> UpdateLoanAsync(UpdateLoanRequest request, CancellationToken cancellationToken);
    Task<LoanDto> AddLoanRateEntryAsync(AddLoanRateEntryRequest request, CancellationToken cancellationToken);
    Task<LoanDto> ApplyLoanPrepaymentAsync(ApplyLoanPrepaymentRequest request, CancellationToken cancellationToken);
    Task<LoanChargeDto> CreateLoanChargeAsync(CreateLoanChargeRequest request, CancellationToken cancellationToken);
    Task<LoanChargeDto> UpdateLoanChargeAsync(UpdateLoanChargeRequest request, CancellationToken cancellationToken);
    Task DeleteLoanChargeAsync(DeleteLoanChargeRequest request, CancellationToken cancellationToken);
    Task DeleteLoanAsync(DeleteLoanRequest request, CancellationToken cancellationToken);
    Task SetLoanInstallmentPaidAsync(SetLoanInstallmentPaidRequest request, CancellationToken cancellationToken);
    Task OverrideLoanInstallmentAsync(OverrideLoanInstallmentRequest request, CancellationToken cancellationToken);
    Task SyncLoanInstallmentsForMonthAsync(int year, int month, CancellationToken cancellationToken);
}