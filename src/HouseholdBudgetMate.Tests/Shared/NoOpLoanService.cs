using HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;

namespace HouseholdBudgetMate.Tests.Shared;

public sealed class NoOpLoanService : ILoanService
{
    public Task<IReadOnlyList<LoanDto>> GetAllAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<LoanDto>>([]);

    public Task<LoanDto> CreateLoanAsync(CreateLoanRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new LoanDto());

    public Task<LoanDto> UpdateLoanAsync(UpdateLoanRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new LoanDto());

    public Task<LoanDto> AddLoanRateEntryAsync(AddLoanRateEntryRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new LoanDto());

    public Task<LoanDto> ApplyLoanPrepaymentAsync(ApplyLoanPrepaymentRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new LoanDto());

    public Task<LoanDto> ApplyLoanInstallmentAmountChangeAsync(ApplyLoanInstallmentAmountChangeRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(new LoanDto());

    public Task<LoanChargeDto> CreateLoanChargeAsync(CreateLoanChargeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new LoanChargeDto());

    public Task<LoanChargeDto> UpdateLoanChargeAsync(UpdateLoanChargeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new LoanChargeDto());

    public Task DeleteLoanChargeAsync(DeleteLoanChargeRequest request, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task DeleteLoanAsync(DeleteLoanRequest request, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task SetLoanInstallmentPaidAsync(SetLoanInstallmentPaidRequest request, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task OverrideLoanInstallmentAsync(OverrideLoanInstallmentRequest request, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task SyncLoanInstallmentsForMonthAsync(int year, int month, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
