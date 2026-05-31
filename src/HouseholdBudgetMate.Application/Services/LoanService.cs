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

namespace HouseholdBudgetMate.Application.Services;

public sealed class LoanService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider) : ILoanService
{
    private static readonly YearMonthRequestValidator YearMonthValidator = new();
    private static readonly CreateLoanRequestValidator CreateLoanValidator = new();
    private static readonly UpdateLoanRequestValidator UpdateLoanValidator = new();
    private static readonly AddLoanRateEntryRequestValidator AddLoanRateEntryValidator = new();
    private static readonly ApplyLoanPrepaymentRequestValidator ApplyLoanPrepaymentValidator = new();
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

        loan.RateEntries.Add(new LoanRateEntry
        {
            LoanId = loan.Id,
            EffectiveFrom = request.EffectiveFrom,
            ReferenceRate = request.ReferenceRate
        });

        await RebuildInstallmentsFromAsync(dbContext, loan, request.EffectiveFrom, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var today = dateTimeProvider.GetLocalDateOnly();
        await SyncLoanInstallmentsForMonthAsync(today.Year, today.Month, cancellationToken);

        return await BuildLoanDtoAsync(dbContext, loan.Id, cancellationToken);
    }

    public async Task<LoanDto> ApplyLoanPrepaymentAsync(ApplyLoanPrepaymentRequest request,
        CancellationToken cancellationToken)
    {
        ApplyLoanPrepaymentValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var loan = await dbContext.Loans
                       .Include(x => x.Tag)
                       .Include(x => x.RateEntries)
                       .Include(x => x.Charges)
                       .Include(x => x.Installments)
                       .ThenInclude(x => x.Expense)
                       .FirstOrDefaultAsync(x => x.Installments.Any(i => i.Id == request.LoanInstallmentId), cancellationToken)
                   ?? throw new NotFoundException("Loan installment not found.");

        var targetInstallment = loan.Installments.First(x => x.Id == request.LoanInstallmentId);
        if (targetInstallment.IsPaid)
        {
            throw new BadRequestException("Cannot apply prepayment to paid installment.");
        }

        var affectedInstallments = loan.Installments
            .Where(x => !x.IsPaid && x.DueDate >= targetInstallment.DueDate)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .ToList();

        if (affectedInstallments.Count == 0)
        {
            throw new BadRequestException("No installments available for prepayment recalculation.");
        }

        var remainingPrincipal = decimal.Round(affectedInstallments.Sum(x => x.PrincipalAmount), 2, MidpointRounding.AwayFromZero);
        if (request.Amount >= remainingPrincipal)
        {
            throw new BadRequestException("Prepayment amount must be lower than remaining principal.");
        }

        var principalAfterPrepayment = decimal.Round(remainingPrincipal - request.Amount, 2, MidpointRounding.AwayFromZero);
        if (principalAfterPrepayment <= 0)
        {
            throw new BadRequestException("Prepayment leaves no principal to recalculate.");
        }

        foreach (var installment in affectedInstallments)
        {
            if (installment.Expense is null)
            {
                continue;
            }

            installment.Expense.IsDeleted = true;
            installment.Expense.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();
        }

        dbContext.LoanInstallments.RemoveRange(affectedInstallments);

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
            loan.EndDate = scheduleEnd;
        }

        var schedule = BuildSchedule(loan, principalAfterPrepayment, scheduleStart, scheduleEnd);
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
                IsPaid = false
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var today = dateTimeProvider.GetLocalDateOnly();
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

            var outstandingBalance = installment.Loan.Installments
                .Where(x => x.DueDate >= installment.DueDate)
                .Sum(x => x.PrincipalAmount);

            var chargeAmount = installment.Loan.Charges
                .Where(x => x.IsActive)
                .Where(x => IsChargeDueInMonth(x, year, month))
                .Sum(x => x.IsPercentageBased
                    ? decimal.Round(outstandingBalance * x.Amount / 100m, 2, MidpointRounding.AwayFromZero)
                    : x.Amount);

            var totalAmount = installment.Amount + chargeAmount;

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

    private async Task RebuildInstallmentsFromAsync(
        ApplicationDbContext dbContext,
        Loan loan,
        DateOnly effectiveFrom,
        CancellationToken cancellationToken)
    {
        var affectedInstallments = loan.Installments
            .Where(x => x.DueDate >= effectiveFrom)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .ToList();

        if (affectedInstallments.Count == 0)
        {
            return;
        }

        var remainingPrincipal = affectedInstallments.Sum(x => x.PrincipalAmount);
        if (remainingPrincipal <= 0)
        {
            return;
        }

        foreach (var installment in affectedInstallments)
        {
            if (installment.Expense is null)
            {
                continue;
            }

            installment.Expense.IsDeleted = true;
            installment.Expense.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();
        }

        dbContext.LoanInstallments.RemoveRange(affectedInstallments);

        var schedule = BuildSchedule(loan, remainingPrincipal, affectedInstallments[0].DueDate, loan.EndDate);
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
                IsPaid = false
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
