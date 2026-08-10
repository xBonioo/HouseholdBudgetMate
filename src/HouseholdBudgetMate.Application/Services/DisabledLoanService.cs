using HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;

namespace HouseholdBudgetMate.Application.Services;

public sealed class DisabledLoanService : ILoanService
{
    private const string DisabledMessage = "Funkcja kredytów jest wyłączona.";

    public Task<IReadOnlyList<LoanDto>> GetAllAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<LoanDto>>([]);

    public Task<DebtSummaryDto> GetDebtSummaryAsync(int year, int month, CancellationToken cancellationToken)
        => Task.FromResult(new DebtSummaryDto());

    public Task<LoanDto> CreateLoanAsync(CreateLoanRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task<LoanDto> UpdateLoanAsync(UpdateLoanRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task<LoanScheduleChangePreviewDto> PreviewAddLoanRateEntryAsync(
        AddLoanRateEntryRequest request,
        CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task<LoanScheduleChangePreviewDto> PreviewApplyLoanPrepaymentAsync(
        ApplyLoanPrepaymentRequest request,
        CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task<LoanScheduleChangePreviewDto> PreviewApplyLoanInstallmentAmountChangeAsync(
        ApplyLoanInstallmentAmountChangeRequest request,
        CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task<LoanDto> AddLoanRateEntryAsync(AddLoanRateEntryRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task<LoanDto> ApplyLoanPrepaymentAsync(ApplyLoanPrepaymentRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task<LoanDto> ApplyLoanInstallmentAmountChangeAsync(
        ApplyLoanInstallmentAmountChangeRequest request,
        CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task<LoanDto> RevertLoanOperationAsync(RevertLoanOperationRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task<LoanChargeDto> CreateLoanChargeAsync(CreateLoanChargeRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task<LoanChargeDto> UpdateLoanChargeAsync(UpdateLoanChargeRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task DeleteLoanChargeAsync(DeleteLoanChargeRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task DeleteLoanAsync(DeleteLoanRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task SetLoanInstallmentPaidAsync(SetLoanInstallmentPaidRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task OverrideLoanInstallmentAsync(OverrideLoanInstallmentRequest request, CancellationToken cancellationToken)
        => throw new UnavailableException(DisabledMessage);

    public Task SyncLoanInstallmentsForMonthAsync(int year, int month, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
