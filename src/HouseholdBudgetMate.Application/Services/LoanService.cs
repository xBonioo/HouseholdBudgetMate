using HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Application.Mapping;
using HouseholdBudgetMate.Application.Validation;
using HouseholdBudgetMate.Application.Validation.Common;
using HouseholdBudgetMate.Application.Validation.Loans;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HouseholdBudgetMate.Application.Services;

public sealed class LoanService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider) : ILoanService
{
    private const string LegacyPrepaymentSuffix = " - nadpłata";

    private static readonly YearMonthRequestValidator YearMonthValidator = new();
    private static readonly CreateLoanRequestValidator CreateLoanValidator = new();
    private static readonly UpdateLoanRequestValidator UpdateLoanValidator = new();
    private static readonly AddLoanRateEntryRequestValidator AddLoanRateEntryValidator = new();
    private static readonly ApplyLoanPrepaymentRequestValidator ApplyLoanPrepaymentValidator = new();
    private static readonly ApplyLoanInstallmentAmountChangeRequestValidator ApplyInstallmentAmountChangeValidator = new();
    private static readonly CreateLoanChargeRequestValidator CreateLoanChargeValidator = new();
    private static readonly UpdateLoanChargeRequestValidator UpdateLoanChargeValidator = new();
    private static readonly DeleteLoanChargeRequestValidator DeleteLoanChargeValidator = new();
    private static readonly DeleteLoanRequestValidator DeleteLoanValidator = new();
    private static readonly SetLoanInstallmentPaidRequestValidator SetInstallmentPaidValidator = new();

    public async Task<IReadOnlyList<LoanDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var loans = await dbContext.Loans
            .AsNoTracking()
            .Include(x => x.Tag)
            .Include(x => x.RateEntries)
            .Include(x => x.Charges)
            .Include(x => x.Installments)
            .ThenInclude(x => x.Expense)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .Select(x => x.MapToDto())
            .ToListAsync(cancellationToken);

        return loans;
    }

    public async Task<DebtSummaryDto> GetDebtSummaryAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var loans = await dbContext.Loans
            .AsNoTracking()
            .Include(x => x.RateEntries)
            .Include(x => x.Installments)
            .ThenInclude(x => x.Expense)
            .ToListAsync(cancellationToken);

        if (loans.Count == 0)
        {
            return new DebtSummaryDto();
        }

        var prepaymentAdjustments = await LoadFuturePrepaymentAdjustmentsAsync(dbContext, loans, year, month, cancellationToken);

        var activeLoans = loans
            .Where(loan => loan.IsActive)
            .Where(loan => IsLoanVisibleInSelectedMonth(loan, monthStart, monthEnd))
            .ToList();

        var activeDebt = activeLoans.Sum(loan =>
        {
            var remainingPrincipal = GetRemainingPrincipalForSelectedMonth(loan, monthEnd);
            var adjustment = prepaymentAdjustments.GetValueOrDefault(loan.Id);
            return decimal.Round(remainingPrincipal + adjustment, 2, MidpointRounding.AwayFromZero);
        });

        return new DebtSummaryDto
        {
            ActiveDebt = decimal.Round(activeDebt, 2, MidpointRounding.AwayFromZero),
            ActiveLoanCount = activeLoans.Count
        };
    }

    public async Task<LoanDto> CreateLoanAsync(CreateLoanRequest request, CancellationToken cancellationToken)
    {
        CreateLoanValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var loanCategoryId = await GetOrCreateLoanCategoryIdAsync(dbContext, cancellationToken);
        await ValidateLoanTagAsync(dbContext, request.TagId, loanCategoryId, cancellationToken);

        var loan = new Loan
        {
            Name = request.Name,
            LoanType = (int)request.LoanType,
            InterestMode = (int)request.InterestMode,
            WiborPeriodType = request.WiborPeriodType is null ? null : (int)request.WiborPeriodType,
            Principal = request.Principal,
            OriginalPrincipal = request.OriginalPrincipal,
            GracePeriodMonths = request.GracePeriodMonths,
            InterestRate = request.InterestRate,
            MarginRate = request.MarginRate,
            RepaymentDayOfMonth = request.RepaymentDayOfMonth,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TagId = request.TagId,
            IsActive = request.IsActive
        };

        if (request.LoanType == LoanType.Mortgage)
        {
            loan.RateEntries.Add(new LoanRateEntry
            {
                EffectiveFrom = ResolveInitialRateEffectiveFrom(request.StartDate, request.InterestMode),
                ReferenceRate = request.InitialReferenceRate ?? 0
            });
        }

        dbContext.Loans.Add(loan);
        await dbContext.SaveChangesAsync(cancellationToken);

        await RegenerateInstallmentsAsync(dbContext, loan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildLoanDtoAsync(dbContext, loan.Id, cancellationToken);
    }

    public async Task<LoanDto> UpdateLoanAsync(UpdateLoanRequest request, CancellationToken cancellationToken)
    {
        UpdateLoanValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var loanCategoryId = await GetOrCreateLoanCategoryIdAsync(dbContext, cancellationToken);
        await ValidateLoanTagAsync(dbContext, request.TagId, loanCategoryId, cancellationToken);

        var loan = await dbContext.Loans
                       .Include(x => x.Tag)
                       .Include(x => x.RateEntries)
                       .Include(x => x.Charges)
                       .Include(x => x.Installments)
                       .ThenInclude(x => x.Expense)
                       .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                   ?? throw new NotFoundException("Loan not found.");

        loan.Name = request.Name;
        loan.TagId = request.TagId;
        loan.IsActive = request.IsActive;
        loan.OriginalPrincipal = request.OriginalPrincipal;
        loan.GracePeriodMonths = request.GracePeriodMonths;

        var hasPaidInstallments = loan.Installments.Any(x => x.IsPaid);
        if (hasPaidInstallments)
        {
            var scheduleChanged = IsScheduleConfigurationChanged(loan, request);
            if (scheduleChanged)
            {
                throw new BadRequestException(
                    "Cannot change schedule settings when loan has paid installments. You can edit name, tag and active status.");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return await BuildLoanDtoAsync(dbContext, loan.Id, cancellationToken);
        }

        loan.LoanType = (int)request.LoanType;
        loan.InterestMode = (int)request.InterestMode;
        loan.WiborPeriodType = request.WiborPeriodType is null ? null : (int)request.WiborPeriodType;
        loan.Principal = request.Principal;
        loan.InterestRate = request.InterestRate;
        loan.MarginRate = request.MarginRate;
        loan.RepaymentDayOfMonth = request.RepaymentDayOfMonth;
        loan.StartDate = request.StartDate;
        loan.EndDate = request.EndDate;

        dbContext.LoanRateEntries.RemoveRange(loan.RateEntries);
        loan.RateEntries.Clear();
        if (request.LoanType == LoanType.Mortgage)
        {
            loan.RateEntries.Add(new LoanRateEntry
            {
                LoanId = loan.Id,
                EffectiveFrom = ResolveInitialRateEffectiveFrom(request.StartDate, request.InterestMode),
                ReferenceRate = request.InitialReferenceRate ?? 0
            });
        }

        foreach (var installment in loan.Installments)
        {
            if (installment.Expense is null)
            {
                continue;
            }

            installment.Expense.IsDeleted = true;
            installment.Expense.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();
        }

        dbContext.LoanInstallments.RemoveRange(loan.Installments);
        await dbContext.SaveChangesAsync(cancellationToken);

        await RegenerateInstallmentsAsync(dbContext, loan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildLoanDtoAsync(dbContext, loan.Id, cancellationToken);
    }

    public async Task<LoanScheduleChangePreviewDto> PreviewAddLoanRateEntryAsync(AddLoanRateEntryRequest request,
        CancellationToken cancellationToken)
    {
        AddLoanRateEntryValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var loan = await LoadLoanForSchedulePreviewAsync(dbContext, request.LoanId, cancellationToken);
        var beforeDto = loan.MapToDto();
        var projection = ProjectRateEntryChange(loan, request);
        ApplyProjectionToPreview(loan, projection);
        var afterDto = loan.MapToDto();

        return BuildLoanScheduleChangePreview(
            beforeDto,
            afterDto,
            loan.Id,
            loan.Name,
            "AddLoanRateEntry",
            "Aktualizacja WIBOR",
            projection.ScheduleStart,
            projection.SourceVersion);
    }

    public async Task<LoanScheduleChangePreviewDto> PreviewApplyLoanPrepaymentAsync(ApplyLoanPrepaymentRequest request,
        CancellationToken cancellationToken)
    {
        ApplyLoanPrepaymentValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var loan = await LoadLoanForSchedulePreviewByInstallmentAsync(
            dbContext,
            request.LoanInstallmentId,
            cancellationToken);
        var beforeDto = loan.MapToDto();
        var projection = ProjectPrepayment(loan, request);
        ApplyProjectionToPreview(loan, projection);
        var afterDto = loan.MapToDto();

        return BuildLoanScheduleChangePreview(
            beforeDto,
            afterDto,
            loan.Id,
            loan.Name,
            "ApplyLoanPrepayment",
            "Nadpłata",
            projection.ScheduleStart,
            projection.SourceVersion);
    }

    public async Task<LoanScheduleChangePreviewDto> PreviewApplyLoanInstallmentAmountChangeAsync(
        ApplyLoanInstallmentAmountChangeRequest request,
        CancellationToken cancellationToken)
    {
        ApplyInstallmentAmountChangeValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var loan = await LoadLoanForSchedulePreviewByInstallmentAsync(
            dbContext,
            request.LoanInstallmentId,
            cancellationToken);
        var beforeDto = loan.MapToDto();
        var projection = ProjectInstallmentAmountChange(loan, request);
        ApplyProjectionToPreview(loan, projection);
        var afterDto = loan.MapToDto();

        return BuildLoanScheduleChangePreview(
            beforeDto,
            afterDto,
            loan.Id,
            loan.Name,
            "ApplyLoanInstallmentAmountChange",
            "Zmiana raty z banku",
            projection.ScheduleStart,
            projection.SourceVersion);
    }

    public async Task<LoanDto> AddLoanRateEntryAsync(AddLoanRateEntryRequest request,
        CancellationToken cancellationToken)
    {
        AddLoanRateEntryValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var loan = await dbContext.Loans
                       .Include(x => x.RateEntries)
                       .Include(x => x.Charges)
                       .Include(x => x.Installments)
                       .ThenInclude(x => x.Expense)
                       .FirstOrDefaultAsync(x => x.Id == request.LoanId, cancellationToken)
                   ?? throw new NotFoundException("Loan not found.");

        var projection = ProjectRateEntryChange(loan, request);
        ValidateExpectedScheduleVersion(projection.SourceVersion, request.ExpectedScheduleVersion);
        loan.RateEntries.Add(projection.RateEntry!);
        PersistProjectedInstallments(dbContext, loan, projection);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SyncOpenLoanInstallmentPlansAsync(projection.ScheduleStart, projection.ScheduleEnd, cancellationToken);

        var today = dateTimeProvider.GetLocalDateOnly();
        await SyncLoanInstallmentsForMonthAsync(today.Year, today.Month, cancellationToken);

        return await BuildLoanDtoAsync(dbContext, loan.Id, cancellationToken);
    }

    public async Task<LoanDto> ApplyLoanPrepaymentAsync(ApplyLoanPrepaymentRequest request,
        CancellationToken cancellationToken)
    {
        ApplyLoanPrepaymentValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var today = dateTimeProvider.GetLocalDateOnly();

        var loan = await dbContext.Loans
                       .Include(x => x.Tag)
                       .Include(x => x.RateEntries)
                       .Include(x => x.Charges)
                       .Include(x => x.Installments)
                       .ThenInclude(x => x.Expense)
                       .FirstOrDefaultAsync(x => x.Installments.Any(i => i.Id == request.LoanInstallmentId), cancellationToken)
                   ?? await LoadLoanForMissingInstallmentConfirmationAsync(
                       dbContext,
                       request.LoanId,
                       request.ExpectedScheduleVersion,
                       cancellationToken);

        var projection = ProjectPrepayment(loan, request);
        ValidateExpectedScheduleVersion(projection.SourceVersion, request.ExpectedScheduleVersion);
        PersistProjectedInstallments(dbContext, loan, projection);
        RecordLoanPrepayment(dbContext, loan.Id, request.Amount, today);

        await UpsertPrepaymentExpenseAsync(dbContext, loan, request.Amount, today, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SyncOpenLoanInstallmentPlansAsync(projection.ScheduleStart, projection.ScheduleEnd, cancellationToken);

        await SyncLoanInstallmentsForMonthAsync(today.Year, today.Month, cancellationToken);

        return await BuildLoanDtoAsync(dbContext, loan.Id, cancellationToken);
    }

    public async Task<LoanDto> ApplyLoanInstallmentAmountChangeAsync(
        ApplyLoanInstallmentAmountChangeRequest request,
        CancellationToken cancellationToken)
    {
        ApplyInstallmentAmountChangeValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var today = dateTimeProvider.GetLocalDateOnly();

        var loan = await dbContext.Loans
                       .Include(x => x.Tag)
                       .Include(x => x.RateEntries)
                       .Include(x => x.Charges)
                       .Include(x => x.Installments)
                       .ThenInclude(x => x.Expense)
                       .FirstOrDefaultAsync(x => x.Installments.Any(i => i.Id == request.LoanInstallmentId), cancellationToken)
                   ?? await LoadLoanForMissingInstallmentConfirmationAsync(
                       dbContext,
                       request.LoanId,
                       request.ExpectedScheduleVersion,
                       cancellationToken);

        var projection = ProjectInstallmentAmountChange(loan, request);
        ValidateExpectedScheduleVersion(projection.SourceVersion, request.ExpectedScheduleVersion);
        PersistProjectedInstallments(dbContext, loan, projection);

        if (projection.PrepaymentAmount > 0)
        {
            RecordLoanPrepayment(dbContext, loan.Id, projection.PrepaymentAmount, today);
            await UpsertPrepaymentExpenseAsync(dbContext, loan, projection.PrepaymentAmount, today, cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        await SyncOpenLoanInstallmentPlansAsync(projection.ScheduleStart, projection.ScheduleEnd, cancellationToken);
        await SyncLoanInstallmentsForMonthAsync(today.Year, today.Month, cancellationToken);

        return await BuildLoanDtoAsync(dbContext, loan.Id, cancellationToken);
    }

    public async Task<LoanChargeDto> CreateLoanChargeAsync(CreateLoanChargeRequest request,
        CancellationToken cancellationToken)
    {
        CreateLoanChargeValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var loanExists = await dbContext.Loans.AnyAsync(x => x.Id == request.LoanId, cancellationToken);
        if (!loanExists)
        {
            throw new NotFoundException("Loan not found.");
        }

        var charge = new LoanCharge
        {
            LoanId = request.LoanId,
            Name = request.Name,
            ChargeType = (int)request.ChargeType,
            FrequencyType = (int)request.FrequencyType,
            Amount = request.Amount,
            IsPercentageBased = request.IsPercentageBased,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive
        };

        dbContext.LoanCharges.Add(charge);
        await dbContext.SaveChangesAsync(cancellationToken);

        var today = dateTimeProvider.GetLocalDateOnly();
        await SyncLoanInstallmentsForMonthAsync(today.Year, today.Month, cancellationToken);

        return charge.MapToDto();
    }

    public async Task<LoanChargeDto> UpdateLoanChargeAsync(UpdateLoanChargeRequest request,
        CancellationToken cancellationToken)
    {
        UpdateLoanChargeValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var charge = await dbContext.LoanCharges
                         .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                     ?? throw new NotFoundException("Loan charge not found.");

        charge.Name = request.Name;
        charge.ChargeType = (int)request.ChargeType;
        charge.FrequencyType = (int)request.FrequencyType;
        charge.Amount = request.Amount;
        charge.IsPercentageBased = request.IsPercentageBased;
        charge.StartDate = request.StartDate;
        charge.EndDate = request.EndDate;
        charge.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        var today = dateTimeProvider.GetLocalDateOnly();
        await SyncLoanInstallmentsForMonthAsync(today.Year, today.Month, cancellationToken);
        return charge.MapToDto();
    }

    public async Task DeleteLoanChargeAsync(DeleteLoanChargeRequest request, CancellationToken cancellationToken)
    {
        DeleteLoanChargeValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var charge = await dbContext.LoanCharges
                         .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                     ?? throw new NotFoundException("Loan charge not found.");

        dbContext.LoanCharges.Remove(charge);
        await dbContext.SaveChangesAsync(cancellationToken);

        var today = dateTimeProvider.GetLocalDateOnly();
        await SyncLoanInstallmentsForMonthAsync(today.Year, today.Month, cancellationToken);
    }

    public async Task DeleteLoanAsync(DeleteLoanRequest request, CancellationToken cancellationToken)
    {
        DeleteLoanValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var loan = await dbContext.Loans
                       .Include(x => x.Charges)
                       .Include(x => x.Installments)
                       .ThenInclude(x => x.Expense)
                       .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                   ?? throw new NotFoundException("Loan not found.");

        if (loan.Installments.Any(x => x.IsPaid))
        {
            throw new BadRequestException("Cannot delete loan with paid installments.");
        }

        foreach (var installment in loan.Installments)
        {
            if (installment.Expense is null)
            {
                continue;
            }

            installment.Expense.IsDeleted = true;
            installment.Expense.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();
        }

        dbContext.LoanInstallments.RemoveRange(loan.Installments);
        dbContext.Loans.Remove(loan);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetLoanInstallmentPaidAsync(SetLoanInstallmentPaidRequest request,
        CancellationToken cancellationToken)
    {
        SetInstallmentPaidValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var installment = await dbContext.LoanInstallments
                              .Include(x => x.Expense)
                              .FirstOrDefaultAsync(x => x.Id == request.LoanInstallmentId, cancellationToken)
                          ?? throw new NotFoundException("Loan installment not found.");

        installment.IsPaid = request.IsPaid;
        installment.PaidAtUtc = request.IsPaid ? dateTimeProvider.GetUtcDateTime() : null;

        if (installment.Expense is not null)
        {
            installment.Expense.ActualAmount = request.IsPaid ? installment.Expense.PlannedAmount : 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task OverrideLoanInstallmentAsync(OverrideLoanInstallmentRequest request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var installment = await dbContext.LoanInstallments
                              .Include(x => x.Expense)
                              .FirstOrDefaultAsync(x => x.Id == request.InstallmentId, cancellationToken)
                          ?? throw new NotFoundException("Loan installment not found.");

        installment.PrincipalAmount = request.PrincipalAmount;
        installment.InterestAmount = request.InterestAmount;
        installment.Amount = request.PrincipalAmount + request.InterestAmount + request.ChargesAmount;

        if (installment.Expense is not null)
        {
            installment.Expense.PlannedAmount = installment.Amount;
            if (installment.IsPaid)
            {
                installment.Expense.ActualAmount = installment.Amount;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncLoanInstallmentsForMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlan = await dbContext.MonthPlans
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month, cancellationToken);
        if (monthPlan is null || monthPlan.IsClosed)
        {
            return;
        }

        var categoryId = await GetOrCreateLoanCategoryIdAsync(dbContext, cancellationToken);

        var installments = await dbContext.LoanInstallments
            .Include(x => x.Loan)
            .ThenInclude(x => x.Charges)
            .Include(x => x.Loan)
            .ThenInclude(x => x.Installments)
            .Where(x => x.Year == year && x.Month == month)
            .Where(x => x.Loan.IsActive)
            .ToListAsync(cancellationToken);

        if (installments.Count == 0)
        {
            return;
        }

        var deletedExpenseInstallmentIds = await dbContext.Expenses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.UserId == dbContext.CurrentBudgetOwnerUserId
                        && x.MonthPlanId == monthPlan.Id
                        && x.LoanInstallmentId.HasValue
                        && x.IsDeleted)
            .Select(x => x.LoanInstallmentId!.Value)
            .ToListAsync(cancellationToken);

        var deletedSet = deletedExpenseInstallmentIds.ToHashSet();

        var existingExpenses = await dbContext.Expenses
            .Where(x => x.MonthPlanId == monthPlan.Id && x.LoanInstallmentId.HasValue)
            .ToListAsync(cancellationToken);

        var existingByInstallmentId = existingExpenses
            .Where(x => x.LoanInstallmentId.HasValue)
            .ToDictionary(x => x.LoanInstallmentId!.Value, x => x);

        var maxOrder = await dbContext.Expenses
            .Where(x => x.MonthPlanId == monthPlan.Id)
            .Select(x => (int?)x.Order)
            .MaxAsync(cancellationToken) ?? 0;

        foreach (var installment in installments.OrderBy(x => x.DueDate).ThenBy(x => x.Id))
        {
            if (deletedSet.Contains(installment.Id))
            {
                continue;
            }

            if (installment.IsPaid)
            {
                continue;
            }

            var outstandingBalance = installment.Loan.Installments
                .Where(x => x.DueDate >= installment.DueDate)
                .Sum(x => x.PrincipalAmount);

            var chargeAmount = installment.Loan.Charges
                .Where(x => x.IsActive)
                .Where(x => IsChargeDueInMonth(x, year, month))
                .Sum(x => x.IsPercentageBased
                    ? decimal.Round(outstandingBalance * x.Amount / 100m, 2, MidpointRounding.AwayFromZero)
                    : x.Amount);

            var totalAmount = decimal.Round(
                installment.PrincipalAmount + installment.InterestAmount + chargeAmount,
                2,
                MidpointRounding.AwayFromZero);

            if (existingByInstallmentId.TryGetValue(installment.Id, out var existingExpense))
            {
                existingExpense.Name = $"{installment.Loan.Name} - rata {installment.Month:D2}/{installment.Year}";
                existingExpense.CategoryId = categoryId;
                existingExpense.TagId = installment.Loan.TagId;
                existingExpense.PlannedAmount = totalAmount;
                existingExpense.ActualAmount = installment.IsPaid ? totalAmount : 0;
                existingExpense.ShowRemainingInUI = false;
                continue;
            }

            maxOrder++;
            dbContext.Expenses.Add(new Expense
            {
                MonthPlanId = monthPlan.Id,
                Order = maxOrder,
                Name = $"{installment.Loan.Name} - rata {installment.Month:D2}/{installment.Year}",
                CategoryId = categoryId,
                TagId = installment.Loan.TagId,
                LoanInstallmentId = installment.Id,
                PlannedAmount = totalAmount,
                ActualAmount = installment.IsPaid ? totalAmount : 0,
                ShowRemainingInUI = false
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncOpenLoanInstallmentPlansAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return;
        }

        var fromMonthKey = (from.Year * 12) + from.Month;
        var toMonthKey = (to.Year * 12) + to.Month;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var openPlans = await dbContext.MonthPlans
            .AsNoTracking()
            .Where(x => !x.IsClosed)
            .Where(x => ((x.Year * 12) + x.Month) >= fromMonthKey)
            .Where(x => ((x.Year * 12) + x.Month) <= toMonthKey)
            .Select(x => new { x.Year, x.Month })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        foreach (var plan in openPlans)
        {
            await SyncLoanInstallmentsForMonthAsync(plan.Year, plan.Month, cancellationToken);
        }
    }

    private static async Task<LoanDto> BuildLoanDtoAsync(
        ApplicationDbContext dbContext,
        int loanId,
        CancellationToken cancellationToken)
    {
        var loan = await dbContext.Loans
                       .AsNoTracking()
                       .Include(x => x.Tag)
                       .Include(x => x.RateEntries)
                       .Include(x => x.Charges)
                       .Include(x => x.Installments)
                       .ThenInclude(x => x.Expense)
                       .FirstOrDefaultAsync(x => x.Id == loanId, cancellationToken)
                   ?? throw new NotFoundException("Loan not found.");

        return loan.MapToDto();
    }

    private async Task<Loan> LoadLoanForSchedulePreviewAsync(
        ApplicationDbContext dbContext,
        int loanId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Loans
                   .AsNoTracking()
                   .Include(x => x.Tag)
                   .Include(x => x.RateEntries)
                   .Include(x => x.Charges)
                   .Include(x => x.Installments)
                   .ThenInclude(x => x.Expense)
               .FirstOrDefaultAsync(x => x.Id == loanId, cancellationToken)
               ?? throw new NotFoundException("Loan not found.");
    }

    private async Task<Loan> LoadLoanForSchedulePreviewByInstallmentAsync(
        ApplicationDbContext dbContext,
        int loanInstallmentId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Loans
                   .AsNoTracking()
                   .Include(x => x.Tag)
                   .Include(x => x.RateEntries)
                   .Include(x => x.Charges)
                   .Include(x => x.Installments)
                   .ThenInclude(x => x.Expense)
                   .FirstOrDefaultAsync(x => x.Installments.Any(i => i.Id == loanInstallmentId), cancellationToken)
               ?? throw new NotFoundException("Loan installment not found.");
    }

    private static ScheduleChangeProjection ProjectRateEntryChange(Loan loan, AddLoanRateEntryRequest request)
    {
        if (loan.LoanType != (int)LoanType.Mortgage)
        {
            throw new BadRequestException("Rate updates are available only for mortgage loans.");
        }

        var variablePhaseStart = GetVariablePhaseStartDate(loan);
        if (request.EffectiveFrom < variablePhaseStart)
        {
            throw new BadRequestException(
                $"Rate updates are available from {variablePhaseStart:yyyy-MM-dd} for this loan.");
        }

        if (loan.RateEntries.Any(x => x.EffectiveFrom == request.EffectiveFrom))
        {
            throw new BadRequestException("Rate entry for this date already exists.");
        }

        if (loan.Installments.Any(x => x.IsPaid && x.DueDate >= request.EffectiveFrom))
        {
            throw new BadRequestException("Cannot change rate for already paid installments.");
        }

        var affectedInstallments = loan.Installments
            .Where(x => x.DueDate >= request.EffectiveFrom)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .ToList();
        var rateEntry = new LoanRateEntry
        {
            LoanId = loan.Id,
            EffectiveFrom = request.EffectiveFrom,
            ReferenceRate = request.ReferenceRate
        };

        IReadOnlyList<ScheduleRowDto> schedule = [];
        if (affectedInstallments.Count > 0)
        {
            var remainingPrincipal = affectedInstallments.Sum(x => x.PrincipalAmount);
            if (remainingPrincipal > 0)
            {
                loan.RateEntries.Add(rateEntry);
                try
                {
                    schedule = BuildSchedule(
                        loan,
                        remainingPrincipal,
                        affectedInstallments[0].DueDate,
                        loan.EndDate);
                }
                finally
                {
                    loan.RateEntries.Remove(rateEntry);
                }
            }
            else
            {
                affectedInstallments = [];
            }
        }

        return new ScheduleChangeProjection(
            ComputeLoanScheduleVersion(loan),
            request.EffectiveFrom,
            loan.EndDate,
            affectedInstallments,
            schedule,
            rateEntry,
            0m);
    }

    private static ScheduleChangeProjection ProjectPrepayment(Loan loan, ApplyLoanPrepaymentRequest request)
    {
        var targetInstallment = loan.Installments.FirstOrDefault(x => x.Id == request.LoanInstallmentId)
            ?? throw new NotFoundException("Loan installment not found.");
        if (targetInstallment.IsPaid)
        {
            throw new BadRequestException("Cannot apply prepayment to paid installment.");
        }

        var affectedInstallments = GetAffectedPreviewInstallments(loan, targetInstallment.DueDate);
        if (affectedInstallments.Count == 0)
        {
            throw new BadRequestException("No installments available for prepayment recalculation.");
        }

        var remainingPrincipal = decimal.Round(
            affectedInstallments.Sum(x => x.PrincipalAmount),
            2,
            MidpointRounding.AwayFromZero);
        if (request.Amount >= remainingPrincipal)
        {
            throw new BadRequestException("Prepayment amount must be lower than remaining principal.");
        }

        var principalAfterPrepayment = decimal.Round(
            remainingPrincipal - request.Amount,
            2,
            MidpointRounding.AwayFromZero);
        if (principalAfterPrepayment <= 0)
        {
            throw new BadRequestException("Prepayment leaves no principal to recalculate.");
        }

        var scheduleStart = affectedInstallments[0].DueDate;
        var scheduleEnd = loan.EndDate;
        if (request.Strategy == LoanPrepaymentStrategyType.ShortenPeriod)
        {
            scheduleEnd = ResolveShortenedEndDate(
                loan,
                principalAfterPrepayment,
                scheduleStart,
                loan.EndDate,
                affectedInstallments[0].Amount);
        }

        var originalEndDate = loan.EndDate;
        loan.EndDate = scheduleEnd;
        IReadOnlyList<ScheduleRowDto> schedule;
        try
        {
            schedule = BuildSchedule(loan, principalAfterPrepayment, scheduleStart, scheduleEnd);
        }
        finally
        {
            loan.EndDate = originalEndDate;
        }

        return new ScheduleChangeProjection(
            ComputeLoanScheduleVersion(loan, includePrepaymentExpenseIdentity: true),
            scheduleStart,
            scheduleEnd,
            affectedInstallments,
            schedule,
            null,
            request.Amount);
    }

    private static ScheduleChangeProjection ProjectInstallmentAmountChange(
        Loan loan,
        ApplyLoanInstallmentAmountChangeRequest request)
    {
        var targetInstallment = loan.Installments.FirstOrDefault(x => x.Id == request.LoanInstallmentId)
            ?? throw new NotFoundException("Loan installment not found.");
        if (targetInstallment.IsPaid)
        {
            throw new BadRequestException("Cannot change installment amount for paid installment.");
        }

        var affectedInstallments = GetAffectedPreviewInstallments(loan, targetInstallment.DueDate);
        if (affectedInstallments.Count == 0)
        {
            throw new BadRequestException("No installments available for amount recalculation.");
        }

        var remainingPrincipal = decimal.Round(
            affectedInstallments.Sum(x => x.PrincipalAmount),
            2,
            MidpointRounding.AwayFromZero);
        var scheduleStart = affectedInstallments[0].DueDate;
        if (request.LastInstallmentDate < scheduleStart)
        {
            throw new BadRequestException("Last installment date must be greater than or equal to schedule start.");
        }

        if (request.LastInstallmentDate > loan.EndDate)
        {
            throw new BadRequestException("Last installment date cannot extend the current loan period.");
        }

        var targetPrincipal = ResolvePrincipalForTargetInstallmentAmount(
            loan,
            request.InstallmentAmount,
            scheduleStart,
            request.LastInstallmentDate,
            remainingPrincipal);
        if (targetPrincipal > remainingPrincipal)
        {
            throw new BadRequestException("Installment amount does not imply a lower remaining principal.");
        }

        var originalEndDate = loan.EndDate;
        loan.EndDate = request.LastInstallmentDate;
        IReadOnlyList<ScheduleRowDto> schedule;
        try
        {
            schedule = BuildSchedule(loan, targetPrincipal, scheduleStart, request.LastInstallmentDate);
        }
        finally
        {
            loan.EndDate = originalEndDate;
        }

        var prepaymentAmount = decimal.Round(remainingPrincipal - targetPrincipal, 2, MidpointRounding.AwayFromZero);

        return new ScheduleChangeProjection(
            ComputeLoanScheduleVersion(loan, includePrepaymentExpenseIdentity: prepaymentAmount > 0),
            scheduleStart,
            request.LastInstallmentDate,
            affectedInstallments,
            schedule,
            null,
            prepaymentAmount);
    }

    private static void ApplyProjectionToPreview(Loan loan, ScheduleChangeProjection projection)
    {
        if (projection.RateEntry is not null)
        {
            loan.RateEntries.Add(projection.RateEntry);
        }

        ApplyPreviewInstallmentRemoval(loan, projection.AffectedInstallments);
        loan.EndDate = projection.ScheduleEnd;
        AddPreviewInstallments(loan, projection.Schedule);
    }

    private void PersistProjectedInstallments(
        ApplicationDbContext dbContext,
        Loan loan,
        ScheduleChangeProjection projection)
    {
        foreach (var installment in projection.AffectedInstallments)
        {
            if (installment.Expense is null)
            {
                continue;
            }

            installment.Expense.IsDeleted = true;
            installment.Expense.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();
        }

        dbContext.LoanInstallments.RemoveRange(projection.AffectedInstallments);
        loan.EndDate = projection.ScheduleEnd;

        foreach (var row in projection.Schedule)
        {
            dbContext.LoanInstallments.Add(new LoanInstallment
            {
                LoanId = loan.Id,
                Year = row.Year,
                Month = row.Month,
                DueDate = row.DueDate,
                Amount = row.Amount,
                PrincipalAmount = row.PrincipalAmount,
                InterestAmount = row.InterestAmount,
                IsPaid = false
            });
        }
    }

    private static LoanScheduleChangePreviewDto BuildLoanScheduleChangePreview(
        LoanDto beforeDto,
        LoanDto afterDto,
        int loanId,
        string loanName,
        string changeType,
        string changeLabel,
        DateOnly affectedFrom,
        string sourceVersion)
    {
        return new LoanScheduleChangePreviewDto
        {
            LoanId = loanId,
            LoanName = loanName,
            ChangeType = changeType,
            ChangeLabel = changeLabel,
            AffectedFrom = affectedFrom,
            SourceVersion = sourceVersion,
            BeforeSummary = BuildLoanScheduleSummary(beforeDto),
            AfterSummary = BuildLoanScheduleSummary(afterDto),
            Rows = BuildLoanScheduleComparisonRows(beforeDto, afterDto)
        };
    }

    private static LoanScheduleSummaryDto BuildLoanScheduleSummary(LoanDto loan)
    {
        var openInstallments = loan.Installments
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Month)
            .ThenBy(x => x.Year)
            .ToList();

        var nextInstallment = openInstallments.FirstOrDefault(x => !x.IsPaid);

        return new LoanScheduleSummaryDto
        {
            RemainingPrincipal = loan.RemainingPrincipal,
            NextInstallment = nextInstallment is null
                ? 0m
                : ToScheduleRow(nextInstallment, openInstallments, loan.Charges).Amount,
            TotalFutureInterest = decimal.Round(
                openInstallments.Where(x => !x.IsPaid).Sum(x => x.InterestAmount),
                2,
                MidpointRounding.AwayFromZero),
            EndDate = loan.EndDate,
            InstallmentCount = loan.Installments.Count
        };
    }

    private static IReadOnlyList<LoanScheduleComparisonRowDto> BuildLoanScheduleComparisonRows(
        LoanDto beforeLoan,
        LoanDto afterLoan)
    {
        var beforeRows = beforeLoan.Installments;
        var afterRows = afterLoan.Installments;

        var beforeByDueDate = beforeRows
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .ToDictionary(x => x.DueDate, x => ToScheduleRow(x, beforeRows, beforeLoan.Charges));

        var afterByDueDate = afterRows
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .ToDictionary(x => x.DueDate, x => ToScheduleRow(x, afterRows, afterLoan.Charges));

        var dueDates = beforeByDueDate.Keys
            .Union(afterByDueDate.Keys)
            .OrderBy(x => x)
            .ToList();

        var rows = new List<LoanScheduleComparisonRowDto>(dueDates.Count);
        foreach (var dueDate in dueDates)
        {
            beforeByDueDate.TryGetValue(dueDate, out var beforeRow);
            afterByDueDate.TryGetValue(dueDate, out var afterRow);

            var state = (beforeRow, afterRow) switch
            {
                (null, not null) => LoanScheduleComparisonRowState.Added,
                (not null, null) => LoanScheduleComparisonRowState.Removed,
                (not null, not null) when beforeRow == afterRow => LoanScheduleComparisonRowState.Unchanged,
                (not null, not null) => LoanScheduleComparisonRowState.Changed,
                _ => LoanScheduleComparisonRowState.Unchanged
            };

            rows.Add(new LoanScheduleComparisonRowDto
            {
                DueDate = dueDate,
                State = state,
                BeforeIsPaid = beforeRows.Any(x => x.DueDate == dueDate && x.IsPaid),
                AfterIsPaid = afterRows.Any(x => x.DueDate == dueDate && x.IsPaid),
                Before = beforeRow,
                After = afterRow
            });
        }

        return rows;
    }

    private static ScheduleRowDto ToScheduleRow(
        LoanInstallmentDto installment,
        IReadOnlyList<LoanInstallmentDto> installments,
        IReadOnlyList<LoanChargeDto> charges)
    {
        var chargeAmount = CalculateChargeAmount(installment, installments, charges);
        var totalAmount = decimal.Round(
            installment.PrincipalAmount + installment.InterestAmount + chargeAmount,
            2,
            MidpointRounding.AwayFromZero);

        return new ScheduleRowDto(
            installment.Year,
            installment.Month,
            installment.DueDate,
            totalAmount,
            installment.PrincipalAmount,
            installment.InterestAmount,
            chargeAmount);
    }

    private static decimal CalculateChargeAmount(
        LoanInstallmentDto installment,
        IReadOnlyList<LoanInstallmentDto> installments,
        IReadOnlyList<LoanChargeDto> charges)
    {
        var outstandingBalance = installments
            .Where(x => x.DueDate >= installment.DueDate)
            .Sum(x => x.PrincipalAmount);

        return decimal.Round(
            charges
                .Where(x => x.IsActive)
                .Where(x => IsChargeDueInMonth(x, installment.Year, installment.Month))
                .Sum(x => x.IsPercentageBased
                    ? decimal.Round(outstandingBalance * x.Amount / 100m, 2, MidpointRounding.AwayFromZero)
                    : x.Amount),
            2,
            MidpointRounding.AwayFromZero);
    }

    private static void AddPreviewInstallments(Loan loan, IEnumerable<ScheduleRowDto> schedule)
    {
        foreach (var row in schedule)
        {
            loan.Installments.Add(new LoanInstallment
            {
                LoanId = loan.Id,
                Year = row.Year,
                Month = row.Month,
                DueDate = row.DueDate,
                Amount = row.Amount,
                PrincipalAmount = row.PrincipalAmount,
                InterestAmount = row.InterestAmount,
                IsPaid = false,
                PaidAtUtc = null
            });
        }
    }

    private static void ApplyPreviewInstallmentRemoval(Loan loan, IReadOnlyCollection<LoanInstallment> affectedInstallments)
    {
        var affectedIds = affectedInstallments.Select(x => x.Id).ToHashSet();
        loan.Installments = loan.Installments
            .Where(x => !affectedIds.Contains(x.Id))
            .ToList();
    }

    private static IReadOnlyList<LoanInstallment> GetAffectedPreviewInstallments(Loan loan, DateOnly scheduleStart)
    {
        return loan.Installments
            .Where(x => !x.IsPaid && x.DueDate >= scheduleStart)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private static async Task<Loan> LoadLoanForMissingInstallmentConfirmationAsync(
        ApplicationDbContext dbContext,
        int? loanId,
        string? expectedScheduleVersion,
        CancellationToken cancellationToken)
    {
        if (!loanId.HasValue)
        {
            throw new NotFoundException("Loan installment not found.");
        }

        var loan = await dbContext.Loans
                       .Include(x => x.Tag)
                       .Include(x => x.RateEntries)
                       .Include(x => x.Charges)
                       .Include(x => x.Installments)
                       .ThenInclude(x => x.Expense)
                       .FirstOrDefaultAsync(x => x.Id == loanId.Value, cancellationToken)
                   ?? throw new NotFoundException("Loan installment not found.");

        ValidateExpectedScheduleVersion(ComputeLoanScheduleVersion(loan), expectedScheduleVersion);
        throw new NotFoundException("Loan installment not found.");
    }

    private static void ValidateExpectedScheduleVersion(string currentVersion, string? expectedScheduleVersion)
    {
        if (string.IsNullOrWhiteSpace(expectedScheduleVersion))
        {
            throw new BadRequestException("Expected schedule version is required.");
        }

        if (!string.Equals(currentVersion, expectedScheduleVersion, StringComparison.Ordinal))
        {
            throw new ConflictException("The loan schedule preview is stale. Please recalculate before confirming.");
        }
    }

    private sealed record ScheduleChangeProjection(
        string SourceVersion,
        DateOnly ScheduleStart,
        DateOnly ScheduleEnd,
        IReadOnlyList<LoanInstallment> AffectedInstallments,
        IReadOnlyList<ScheduleRowDto> Schedule,
        LoanRateEntry? RateEntry,
        decimal PrepaymentAmount);

    private static string ComputeLoanScheduleVersion(Loan loan, bool includePrepaymentExpenseIdentity = false)
    {
        var builder = new StringBuilder();
        AppendLoanSnapshot(builder, loan);
        if (includePrepaymentExpenseIdentity)
        {
            builder.Append("P|")
                .Append(loan.Name).Append('|')
                .Append(loan.TagId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
                .AppendLine();
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }

    private static void AppendLoanSnapshot(StringBuilder builder, Loan loan)
    {
        builder.Append(loan.LoanType).Append('|')
            .Append(loan.InterestMode).Append('|')
            .Append(loan.WiborPeriodType?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append('|')
            .Append(loan.Principal.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(loan.OriginalPrincipal?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append('|')
            .Append(loan.GracePeriodMonths?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append('|')
            .Append(loan.InterestRate.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(loan.MarginRate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append('|')
            .Append(loan.RepaymentDayOfMonth).Append('|')
            .Append(loan.StartDate.ToString("O", CultureInfo.InvariantCulture)).Append('|')
            .Append(loan.EndDate.ToString("O", CultureInfo.InvariantCulture)).AppendLine();

        foreach (var rateEntry in loan.RateEntries
                     .OrderBy(x => x.EffectiveFrom)
                     .ThenBy(x => x.Id))
        {
            builder.Append("R|")
                .Append(rateEntry.EffectiveFrom.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(rateEntry.ReferenceRate.ToString(CultureInfo.InvariantCulture)).AppendLine();
        }

        foreach (var charge in loan.Charges
                     .OrderBy(x => x.ChargeType)
                     .ThenBy(x => x.FrequencyType)
                     .ThenBy(x => x.StartDate)
                     .ThenBy(x => x.EndDate)
                     .ThenBy(x => x.Amount)
                     .ThenBy(x => x.IsPercentageBased)
                     .ThenBy(x => x.IsActive)
                     .ThenBy(x => x.Id))
        {
            builder.Append("C|")
                .Append(charge.ChargeType).Append('|')
                .Append(charge.FrequencyType).Append('|')
                .Append(charge.Amount.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(charge.IsPercentageBased).Append('|')
                .Append(charge.StartDate.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(charge.EndDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty).Append('|')
                .Append(charge.IsActive).AppendLine();
        }

        foreach (var installment in loan.Installments
                     .OrderBy(x => x.DueDate)
                     .ThenBy(x => x.Id))
        {
            builder.Append("I|")
                .Append(installment.DueDate.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(installment.Amount.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(installment.PrincipalAmount.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(installment.InterestAmount.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(installment.IsPaid).Append('|')
                .Append(installment.PaidAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty).AppendLine();
        }
    }

    private static async Task UpsertPrepaymentExpenseAsync(
        ApplicationDbContext dbContext,
        Loan loan,
        decimal amount,
        DateOnly prepaymentDate,
        CancellationToken cancellationToken)
    {
        var monthPlan = await dbContext.MonthPlans
            .FirstOrDefaultAsync(x => x.Year == prepaymentDate.Year && x.Month == prepaymentDate.Month, cancellationToken);
        var monthPlanWasCreated = false;

        if (monthPlan is null)
        {
            monthPlan = new MonthPlan
            {
                Year = prepaymentDate.Year,
                Month = prepaymentDate.Month,
                IsClosed = false
            };
            dbContext.MonthPlans.Add(monthPlan);
            monthPlanWasCreated = true;
        }

        if (monthPlan.IsClosed)
        {
            throw new BadRequestException("Month is closed. Editing is disabled.");
        }

        var categoryId = await GetOrCreateLoanCategoryIdAsync(dbContext, cancellationToken);
        var expenseName = $"{loan.Name} - nadpłata";
        var existingExpense = monthPlanWasCreated
            ? null
            : await FindExistingPrepaymentExpenseAsync(
                dbContext,
                monthPlan.Id,
                categoryId,
                loan,
                expenseName,
                prepaymentDate,
                cancellationToken);

        if (existingExpense is not null)
        {
            existingExpense.ActualAmount += amount;
            return;
        }

        var nextOrder = monthPlanWasCreated
            ? 1
            : await dbContext.Expenses
                .Where(x => x.MonthPlanId == monthPlan.Id)
                .Select(x => (int?)x.Order)
                .MaxAsync(cancellationToken) + 1 ?? 1;

        dbContext.Expenses.Add(new Expense
        {
            MonthPlan = monthPlan,
            Order = nextOrder,
            Name = expenseName,
            CategoryId = categoryId,
            TagId = loan.TagId,
            PlannedAmount = 0,
            ActualAmount = amount,
            ShowRemainingInUI = true
        });
    }

    private static async Task<Expense?> FindExistingPrepaymentExpenseAsync(
        ApplicationDbContext dbContext,
        int monthPlanId,
        int categoryId,
        Loan loan,
        string expenseName,
        DateOnly prepaymentDate,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.Expenses
            .Where(x => x.MonthPlanId == monthPlanId)
            .Where(x => x.CategoryId == categoryId)
            .Where(x => x.LoanInstallmentId == null)
            .Where(x => x.RegularExpenseDefinitionId == null)
            .Where(x => x.Name.EndsWith(LegacyPrepaymentSuffix))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var currentMetadataMatch = candidates.FirstOrDefault(x =>
            x.Name == expenseName
            && (loan.TagId.HasValue ? x.TagId == loan.TagId.Value : x.TagId == null));
        if (currentMetadataMatch is not null)
        {
            return currentMetadataMatch;
        }

        var hasPreviousPrepaymentForLoanMonth = await dbContext.LoanPrepayments
            .AsNoTracking()
            .AnyAsync(
                x => x.LoanId == loan.Id
                     && x.PrepaymentDate.Year == prepaymentDate.Year
                     && x.PrepaymentDate.Month == prepaymentDate.Month,
                cancellationToken);
        if (hasPreviousPrepaymentForLoanMonth)
        {
            throw new ConflictException(
                "The loan prepayment expense identity changed. Please recalculate before confirming.");
        }

        return null;
    }

    private static async Task RegenerateInstallmentsAsync(
        ApplicationDbContext dbContext,
        Loan loan,
        CancellationToken cancellationToken)
    {
        var schedule = BuildSchedule(loan, loan.Principal, loan.StartDate, loan.EndDate);

        foreach (var row in schedule)
        {
            dbContext.LoanInstallments.Add(new LoanInstallment
            {
                LoanId = loan.Id,
                Year = row.Year,
                Month = row.Month,
                DueDate = row.DueDate,
                Amount = row.Amount,
                PrincipalAmount = row.PrincipalAmount,
                InterestAmount = row.InterestAmount,
                IsPaid = false,
                PaidAtUtc = null
            });
        }
    }

    private static List<ScheduleRowDto> BuildSchedule(Loan loan, decimal principal, DateOnly segmentStart,
        DateOnly segmentEnd)
    {
        var dueDates = BuildDueDates(loan.RepaymentDayOfMonth, segmentStart, segmentEnd);
        if (dueDates.Count == 0)
        {
            throw new BadRequestException("Loan schedule must contain at least one month.");
        }

        if (loan.LoanType == (int)LoanType.Mortgage && loan.InterestMode == (int)LoanInterestMode.Fixed)
        {
            return BuildMortgageFixedThenVariableSchedule(loan, principal, dueDates, segmentStart);
        }

        return loan.InterestMode switch
        {
            (int)LoanInterestMode.Fixed => BuildFixedSchedule(principal, dueDates, loan.InterestRate),
            (int)LoanInterestMode.VariableWibor => BuildVariableSchedule(loan, principal, dueDates, segmentStart),
            _ => throw new BadRequestException("Unsupported loan interest mode.")
        };
    }

    private static List<ScheduleRowDto> BuildMortgageFixedThenVariableSchedule(Loan loan, decimal principal,
        IReadOnlyList<DateOnly> dueDates, DateOnly segmentStart)
    {
        if (!loan.MarginRate.HasValue || !loan.WiborPeriodType.HasValue)
        {
            throw new BadRequestException("Variable mortgage requires margin and WIBOR period.");
        }

        var entries = loan.RateEntries.ToList();
        if (entries.Count == 0)
        {
            throw new BadRequestException("Variable mortgage requires at least one WIBOR rate entry.");
        }

        var variableStart = GetVariablePhaseStartDate(loan);
        var periodMonths = loan.WiborPeriodType.Value;
        var rows = new List<ScheduleRowDto>(dueDates.Count);
        var remaining = principal;
        decimal currentAnnualRate = 0;
        decimal currentInstallment = 0;

        for (var i = 0; i < dueDates.Count; i++)
        {
            var due = dueDates[i];
            var inVariablePhase = due >= variableStart;
            var shouldRecalculateInstallment = i == 0;

            if (inVariablePhase)
            {
                var variableMonthIndex = GetMonthDifference(variableStart, due);
                if (variableMonthIndex % periodMonths == 0)
                {
                    shouldRecalculateInstallment = true;
                }

                if (shouldRecalculateInstallment)
                {
                    var referenceRate = GetReferenceRateForDate(entries, due);
                    currentAnnualRate = referenceRate + loan.MarginRate.Value;
                }
            }
            else if (shouldRecalculateInstallment)
            {
                currentAnnualRate = loan.InterestRate;
            }

            var prevDate = i == 0 ? segmentStart : dueDates[i - 1];
            var daysInPeriod = due.DayNumber - prevDate.DayNumber;
            if (i == 0 && daysInPeriod <= 0)
            {
                prevDate = due.AddMonths(-1);
                daysInPeriod = due.DayNumber - prevDate.DayNumber;
            }
            var monthlyRate = currentAnnualRate / 36500m * daysInPeriod;

            if (shouldRecalculateInstallment)
            {
                var pmtRate = currentAnnualRate / 12m / 100m;
                var remainingMonths = dueDates.Count - i;
                currentInstallment = CalculateInstallment(remaining, pmtRate, remainingMonths);
            }

            var interest = decimal.Round(remaining * monthlyRate, 2, MidpointRounding.AwayFromZero);
            decimal principalPart;

            if (i == dueDates.Count - 1)
            {
                principalPart = remaining;
            }
            else
            {
                // Broken-period (spezzato) convention: when the first installment covers fewer days
                // than the standard full-month period, capital is calculated using standard-period
                // interest so the amortization schedule is not skewed by the short first period.
                var standardDays = DateTime.DaysInMonth(prevDate.Year, prevDate.Month);
                if (i == 0 && daysInPeriod < standardDays)
                {
                    var standardInterest = remaining * currentAnnualRate / 36500m * standardDays;
                    principalPart = decimal.Round(currentInstallment - standardInterest, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    principalPart = decimal.Round(currentInstallment - interest, 2, MidpointRounding.AwayFromZero);
                }
            }

            var amount = decimal.Round(principalPart + interest, 2, MidpointRounding.AwayFromZero);
            remaining = decimal.Round(remaining - principalPart, 2, MidpointRounding.AwayFromZero);
            rows.Add(new ScheduleRowDto(due.Year, due.Month, due, amount, principalPart, interest));
        }

        return rows;
    }

    private static List<ScheduleRowDto> BuildFixedSchedule(decimal principal, IReadOnlyList<DateOnly> dueDates,
        decimal annualRate)
    {
        var months = dueDates.Count;
        var monthlyRate = annualRate / 12m / 100m;
        var remaining = principal;

        var monthlyInstallment = CalculateInstallment(remaining, monthlyRate, months);
        var rows = new List<ScheduleRowDto>(months);

        for (var i = 0; i < months; i++)
        {
            var due = dueDates[i];
            var interest = decimal.Round(remaining * monthlyRate, 2, MidpointRounding.AwayFromZero);
            var principalPart = decimal.Round(monthlyInstallment - interest, 2, MidpointRounding.AwayFromZero);

            if (i == months - 1)
            {
                principalPart = remaining;
            }

            var amount = decimal.Round(principalPart + interest, 2, MidpointRounding.AwayFromZero);
            remaining = decimal.Round(remaining - principalPart, 2, MidpointRounding.AwayFromZero);

            rows.Add(new ScheduleRowDto(due.Year, due.Month, due, amount, principalPart, interest));
        }

        return rows;
    }

    private static List<ScheduleRowDto> BuildVariableSchedule(Loan loan, decimal principal,
        IReadOnlyList<DateOnly> dueDates, DateOnly segmentStart)
    {
        if (!loan.MarginRate.HasValue || !loan.WiborPeriodType.HasValue)
        {
            throw new BadRequestException("Variable mortgage requires margin and WIBOR period.");
        }

        var entries = loan.RateEntries.ToList();
        if (entries.Count == 0)
        {
            throw new BadRequestException("Variable mortgage requires at least one WIBOR rate entry.");
        }

        var months = dueDates.Count;
        var remaining = principal;
        var rows = new List<ScheduleRowDto>(months);
        decimal currentInstallment = 0;
        decimal currentAnnualRate = 0;
        decimal accumulatedPrincipalRoundingDelta = 0;

        for (var i = 0; i < months; i++)
        {
            var due = dueDates[i];

            var prevDate = i == 0 ? segmentStart : dueDates[i - 1];
            var daysInPeriod = due.DayNumber - prevDate.DayNumber;
            if (i == 0 && daysInPeriod <= 0)
            {
                prevDate = due.AddMonths(-1);
                daysInPeriod = due.DayNumber - prevDate.DayNumber;
            }

            var referenceRate = GetReferenceRateForDate(entries, due);
            var annualRate = referenceRate + loan.MarginRate.Value;
            var shouldRecalculateInstallment = i == 0 || annualRate != currentAnnualRate;
            currentAnnualRate = annualRate;

            if (shouldRecalculateInstallment)
            {
                var pmtRate = currentAnnualRate / 12m / 100m;
                var remainingMonths = months - i;
                currentInstallment = CalculateInstallment(remaining, pmtRate, remainingMonths);
            }

            var interestRate = currentAnnualRate / 36500m * daysInPeriod;
            var rawInterest = remaining * interestRate;
            var interest = decimal.Round(rawInterest, 2, MidpointRounding.AwayFromZero);
            decimal principalPartRaw;
            decimal principalPart;

            if (i == months - 1)
            {
                principalPart = decimal.Round(remaining + accumulatedPrincipalRoundingDelta,
                    2, MidpointRounding.AwayFromZero);
            }
            else
            {
                // Broken-period (spezzato) convention: when the first installment covers fewer days
                // than the standard full-month period, capital is calculated using standard-period
                // interest so the amortization schedule is not skewed by the short first period.
                var standardDays = DateTime.DaysInMonth(prevDate.Year, prevDate.Month);
                if (i == 0 && daysInPeriod < standardDays)
                {
                    var standardInterest = remaining * currentAnnualRate / 36500m * standardDays;
                    principalPartRaw = currentInstallment - standardInterest;
                    principalPart = decimal.Round(principalPartRaw, 2, MidpointRounding.ToEven);
                    accumulatedPrincipalRoundingDelta += principalPartRaw - principalPart;
                }
                else
                {
                    principalPartRaw = currentInstallment - interest;
                    principalPart = decimal.Round(principalPartRaw, 2, MidpointRounding.ToEven);
                    accumulatedPrincipalRoundingDelta += principalPartRaw - principalPart;
                }
            }

            var amount = decimal.Round(principalPart + interest, 2, MidpointRounding.AwayFromZero);
            remaining = decimal.Round(remaining - principalPart, 2, MidpointRounding.AwayFromZero);
            if (i == months - 1)
            {
                remaining = 0;
            }

            rows.Add(new ScheduleRowDto(due.Year, due.Month, due, amount, principalPart, interest));
        }

        return rows;
    }

    private static DateOnly ResolveInitialRateEffectiveFrom(DateOnly startDate, LoanInterestMode interestMode)
    {
        return interestMode == LoanInterestMode.VariableWibor ? startDate : startDate.AddYears(5);
    }

    private static DateOnly GetVariablePhaseStartDate(Loan loan)
    {
        return loan.InterestMode == (int)LoanInterestMode.VariableWibor ? loan.StartDate : loan.StartDate.AddYears(5);
    }

    private static List<DateOnly> BuildDueDates(int repaymentDayOfMonth, DateOnly segmentStart, DateOnly segmentEnd)
    {
        var months = ((segmentEnd.Year - segmentStart.Year) * 12) + segmentEnd.Month - segmentStart.Month + 1;
        if (months <= 0)
        {
            return [];
        }

        var dueDates = new List<DateOnly>(months);
        for (var i = 0; i < months; i++)
        {
            var month = segmentStart.AddMonths(i);
            var due = CreateDueDate(month.Year, month.Month, repaymentDayOfMonth);
            if (due < segmentStart || due > segmentEnd)
            {
                continue;
            }

            dueDates.Add(due);
        }

        return dueDates;
    }

    private static int GetMonthDifference(DateOnly from, DateOnly to)
    {
        return ((to.Year - from.Year) * 12) + to.Month - from.Month;
    }

    private static DateOnly CreateDueDate(int year, int month, int repaymentDayOfMonth)
    {
        var lastDay = DateTime.DaysInMonth(year, month);
        var day = Math.Clamp(repaymentDayOfMonth, 1, lastDay);
        return new DateOnly(year, month, day);
    }

    private static decimal GetReferenceRateForDate(IEnumerable<LoanRateEntry> entries, DateOnly dueDate)
    {
        var entry = entries
            .Where(x => x.EffectiveFrom <= dueDate)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefault();

        if (entry is null)
        {
            throw new BadRequestException("Missing WIBOR rate entry for repayment month.");
        }

        return entry.ReferenceRate;
    }

    private static decimal CalculateInstallment(decimal principal, decimal monthlyRate, int months)
    {
        if (months <= 0)
        {
            throw new BadRequestException("Loan schedule must contain at least one month.");
        }

        if (monthlyRate == 0)
        {
            return decimal.Round(principal / months, 2, MidpointRounding.AwayFromZero);
        }

        var rate = (double)monthlyRate;
        var factor = Math.Pow(1 + rate, months);
        return decimal.Round(principal * (decimal)(rate * factor / (factor - 1)), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ResolvePrincipalForTargetInstallmentAmount(
        Loan loan,
        decimal targetInstallmentAmount,
        DateOnly scheduleStart,
        DateOnly scheduleEnd,
        decimal maxPrincipal)
    {
        var maxSchedule = BuildSchedule(loan, maxPrincipal, scheduleStart, scheduleEnd);
        var maxInstallmentAmount = maxSchedule[0].Amount;
        if (targetInstallmentAmount > maxInstallmentAmount)
        {
            var suggestedLastInstallmentDate = FindSuggestedLastInstallmentDate(
                loan,
                targetInstallmentAmount,
                scheduleStart,
                scheduleEnd,
                maxPrincipal);

            var message = suggestedLastInstallmentDate.HasValue
                ? $"Kwota raty jest zbyt wysoka dla wybranej daty ostatniej raty. Spróbuj podać wcześniejszą datę ostatniej raty, na przykład {suggestedLastInstallmentDate:yyyy-MM-dd}."
                : "Kwota raty jest zbyt wysoka dla wybranej daty ostatniej raty.";

            throw new BadRequestException(message);
        }

        if (targetInstallmentAmount == maxInstallmentAmount)
        {
            return maxPrincipal;
        }

        var low = 0m;
        var high = maxPrincipal;
        for (var i = 0; i < 80; i++)
        {
            var mid = (low + high) / 2m;
            var probe = BuildSchedule(loan, mid, scheduleStart, scheduleEnd);
            if (probe[0].Amount > targetInstallmentAmount)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }

        return decimal.Round(low, 2, MidpointRounding.AwayFromZero);
    }

    private static DateOnly? FindSuggestedLastInstallmentDate(
        Loan loan,
        decimal targetInstallmentAmount,
        DateOnly scheduleStart,
        DateOnly selectedLastInstallmentDate,
        decimal maxPrincipal)
    {
        for (var candidate = selectedLastInstallmentDate.AddMonths(-1); candidate >= scheduleStart; candidate = candidate.AddMonths(-1))
        {
            var schedule = BuildSchedule(loan, maxPrincipal, scheduleStart, candidate);
            if (schedule[0].Amount >= targetInstallmentAmount)
            {
                return candidate;
            }
        }

        return null;
    }

    private static DateOnly ResolveShortenedEndDate(
        Loan loan,
        decimal principal,
        DateOnly scheduleStart,
        DateOnly maxEndDate,
        decimal baselineInstallmentAmount)
    {
        var candidateEnd = maxEndDate;
        while (candidateEnd > scheduleStart)
        {
            var probeSchedule = BuildSchedule(loan, principal, scheduleStart, candidateEnd);
            if (probeSchedule[0].Amount >= baselineInstallmentAmount)
            {
                return candidateEnd;
            }

            candidateEnd = candidateEnd.AddMonths(-1);
        }

        return scheduleStart;
    }

    private static bool IsChargeDueInMonth(LoanCharge charge, int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        if (charge.StartDate > monthEnd || (charge.EndDate.HasValue && charge.EndDate.Value < monthStart))
        {
            return false;
        }

        return charge.FrequencyType switch
        {
            (int)LoanChargeFrequencyType.OneTime => charge.StartDate.Year == year && charge.StartDate.Month == month,
            (int)LoanChargeFrequencyType.Monthly => true,
            (int)LoanChargeFrequencyType.Yearly => charge.StartDate.Month == month && year >= charge.StartDate.Year,
            _ => false
        };
    }

    private static bool IsChargeDueInMonth(LoanChargeDto charge, int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        if (charge.StartDate > monthEnd || (charge.EndDate.HasValue && charge.EndDate.Value < monthStart))
        {
            return false;
        }

        return charge.FrequencyType switch
        {
            LoanChargeFrequencyType.OneTime => charge.StartDate.Year == year && charge.StartDate.Month == month,
            LoanChargeFrequencyType.Monthly => true,
            LoanChargeFrequencyType.Yearly => charge.StartDate.Month == month && year >= charge.StartDate.Year,
            _ => false
        };
    }

    private static bool IsScheduleConfigurationChanged(Loan loan, UpdateLoanRequest request)
    {
        return loan.LoanType != (int)request.LoanType
               || loan.InterestMode != (int)request.InterestMode
               || loan.WiborPeriodType != (request.WiborPeriodType is null ? null : (int)request.WiborPeriodType)
               || loan.Principal != request.Principal
               || loan.InterestRate != request.InterestRate
               || loan.MarginRate != request.MarginRate
               || loan.StartDate != request.StartDate
               || loan.EndDate != request.EndDate;
    }

    private static async Task<int> GetOrCreateLoanCategoryIdAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == "Kredyt", cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var category = new Category
        {
            Name = "Kredyt",
            Color = "#EB5757",
            SupportsLineItems = false,
            IsDeleted = false
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    private static async Task<Dictionary<int, decimal>> LoadFuturePrepaymentAdjustmentsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<Loan> loans,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var adjustments = await dbContext.LoanPrepayments
            .AsNoTracking()
            .Where(x => x.PrepaymentDate.Year > year || (x.PrepaymentDate.Year == year && x.PrepaymentDate.Month > month))
            .Select(x => new
            {
                x.LoanId,
                x.PrepaymentDate,
                x.Amount
            })
            .ToListAsync(cancellationToken);

        var legacyExpenses = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.MonthPlan.Year > year || (x.MonthPlan.Year == year && x.MonthPlan.Month > month))
            .Where(x => x.Category.Name == "Kredyt")
            .Where(x => x.LoanInstallmentId == null)
            .Where(x => x.RegularExpenseDefinitionId == null)
            .Where(x => x.ActualAmount > 0)
            .Where(x => x.PlannedAmount == 0)
            .Where(x => x.ShowRemainingInUI)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.TagId,
                x.ActualAmount,
                x.MonthPlan.Year,
                x.MonthPlan.Month
            })
            .ToListAsync(cancellationToken);

        var representedPrepayments = adjustments
            .GroupBy(x => (x.LoanId, x.PrepaymentDate.Year, x.PrepaymentDate.Month, x.Amount))
            .ToDictionary(x => x.Key, x => x.Count());
        var representedMonthlyTotals = adjustments
            .GroupBy(x => (x.LoanId, x.PrepaymentDate.Year, x.PrepaymentDate.Month))
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));

        var fallbackAdjustments = legacyExpenses
            .Where(x => x.Name.Contains("nadpłata", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Id)
            .Select(x => new LegacyPrepaymentExpense(
                x.Id,
                x.Name,
                x.TagId,
                x.ActualAmount,
                new DateOnly(x.Year, x.Month, 1)))
            .Select(x => ResolveLegacyPrepaymentAdjustment(x, loans, representedPrepayments, representedMonthlyTotals))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        return adjustments
            .Select(x => new PrepaymentAdjustment(x.LoanId, x.Amount))
            .Concat(fallbackAdjustments)
            .GroupBy(x => x.LoanId)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));
    }

    private static PrepaymentAdjustment? ResolveLegacyPrepaymentAdjustment(
        LegacyPrepaymentExpense expense,
        IReadOnlyList<Loan> loans,
        Dictionary<(int LoanId, int Year, int Month, decimal Amount), int> representedPrepayments,
        IReadOnlyDictionary<(int LoanId, int Year, int Month), decimal> representedMonthlyTotals)
    {
        var loan = ResolveLegacyPrepaymentLoan(expense, loans);
        if (loan is null)
        {
            return null;
        }

        var representedKey = (loan.Id, expense.PrepaymentDate.Year, expense.PrepaymentDate.Month, expense.Amount);
        if (representedPrepayments.TryGetValue(representedKey, out var representedCount) && representedCount > 0)
        {
            representedPrepayments[representedKey] = representedCount - 1;
            return null;
        }

        var representedMonthKey = (loan.Id, expense.PrepaymentDate.Year, expense.PrepaymentDate.Month);
        if (representedMonthlyTotals.TryGetValue(representedMonthKey, out var representedMonthTotal)
            && representedMonthTotal == expense.Amount)
        {
            return null;
        }

        return new PrepaymentAdjustment(loan.Id, expense.Amount);
    }

    private static Loan? ResolveLegacyPrepaymentLoan(LegacyPrepaymentExpense expense, IReadOnlyList<Loan> loans)
    {
        var exactName = TryGetLegacyPrepaymentLoanName(expense.Name);
        if (exactName is not null)
        {
            var nameMatches = loans
                .Where(x => string.Equals(x.Name, exactName, StringComparison.Ordinal))
                .ToList();

            if (nameMatches.Count == 1)
            {
                return nameMatches[0];
            }

            if (expense.TagId.HasValue)
            {
                var taggedNameMatches = nameMatches
                    .Where(x => x.TagId == expense.TagId)
                    .ToList();

                if (taggedNameMatches.Count == 1)
                {
                    return taggedNameMatches[0];
                }
            }
        }

        return null;
    }

    private static string? TryGetLegacyPrepaymentLoanName(string expenseName)
    {
        return expenseName.EndsWith(LegacyPrepaymentSuffix, StringComparison.Ordinal)
            ? expenseName[..^LegacyPrepaymentSuffix.Length]
            : null;
    }

    private static void RecordLoanPrepayment(
        ApplicationDbContext dbContext,
        int loanId,
        decimal amount,
        DateOnly prepaymentDate)
    {
        dbContext.LoanPrepayments.Add(new LoanPrepayment
        {
            LoanId = loanId,
            PrepaymentDate = prepaymentDate,
            Amount = amount
        });
    }

    private sealed record LegacyPrepaymentExpense(
        int Id,
        string Name,
        int? TagId,
        decimal Amount,
        DateOnly PrepaymentDate);

    private sealed record PrepaymentAdjustment(int LoanId, decimal Amount);

    private static bool IsLoanVisibleInSelectedMonth(
        Loan loan,
        DateOnly monthStart,
        DateOnly monthEnd)
    {
        var hasStartedBySelectedMonth = loan.StartDate <= monthEnd;

        var hasNotEndedBeforeSelectedMonth = !loan.Installments.Any()
            || loan.Installments.Any(installment => installment.DueDate >= monthStart);

        return hasStartedBySelectedMonth && hasNotEndedBeforeSelectedMonth;
    }

    private static decimal GetRemainingPrincipalForSelectedMonth(Loan loan, DateOnly monthEnd)
    {
        return decimal.Round(
            loan.Installments
                .Where(installment => installment.DueDate > monthEnd)
                .Sum(installment => installment.PrincipalAmount),
            2,
            MidpointRounding.AwayFromZero);
    }

    private static async Task ValidateLoanTagAsync(
        ApplicationDbContext dbContext,
        int? tagId,
        int loanCategoryId,
        CancellationToken cancellationToken)
    {
        if (!tagId.HasValue)
        {
            return;
        }

        var tag = await dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tagId.Value, cancellationToken)
            ?? throw new NotFoundException("Loan tag not found.");

        if (tag.CategoryId != loanCategoryId)
        {
            throw new BadRequestException("Loan tag must belong to 'Kredyt' category.");
        }
    }
}
