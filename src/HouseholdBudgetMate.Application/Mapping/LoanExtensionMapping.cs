using HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Domain.Entities;

namespace HouseholdBudgetMate.Application.Mapping;

public static class LoanExtensionMapping
{
    public static LoanDto MapToDto(this Loan loan)
    {
        return new LoanDto
        {
            Id = loan.Id,
            Name = loan.Name,
            LoanType = ParseType(loan.LoanType),
            InterestMode = ParseInterestMode(loan.InterestMode),
            WiborPeriodType = ParseWiborPeriodType(loan.WiborPeriodType),
            Principal = loan.Principal,
            OriginalPrincipal = loan.OriginalPrincipal,
            GracePeriodMonths = loan.GracePeriodMonths,
            RemainingPrincipal = decimal.Round(
                loan.Installments
                    .Where(x => !x.IsPaid)
                    .Sum(x => x.PrincipalAmount),
                2,
                MidpointRounding.AwayFromZero),
            InterestRate = loan.InterestRate,
            MarginRate = loan.MarginRate,
            CurrentReferenceRate = loan.RateEntries
                .OrderByDescending(x => x.EffectiveFrom)
                .Select(x => (decimal?)x.ReferenceRate)
                .FirstOrDefault(),
            RepaymentDayOfMonth = loan.RepaymentDayOfMonth,
            StartDate = loan.StartDate,
            EndDate = loan.EndDate,
            TagId = loan.TagId,
            TagName = loan.Tag?.Name,
            IsActive = loan.IsActive,
            RateEntries = loan.RateEntries
                .OrderByDescending(x => x.EffectiveFrom)
                .Select(x => x.MapToDto())
                .ToList(),
            Charges = loan.Charges
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Select(x => x.MapToDto())
                .ToList(),
            Installments = loan.Installments
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .Select(x =>
                {
                    var outstandingBalance = loan.Installments
                        .Where(i => i.DueDate >= x.DueDate)
                        .Sum(i => i.PrincipalAmount);
                    return x.MapToDto(loan.Charges, outstandingBalance);
                })
                .ToList()
        };
    }

    public static LoanChargeDto MapToDto(this LoanCharge charge)
    {
        return new LoanChargeDto
        {
            Id = charge.Id,
            LoanId = charge.LoanId,
            Name = charge.Name,
            ChargeType = ParseChargeType(charge.ChargeType),
            FrequencyType = ParseFrequencyType(charge.FrequencyType),
            Amount = charge.Amount,
            IsPercentageBased = charge.IsPercentageBased,
            StartDate = charge.StartDate,
            EndDate = charge.EndDate,
            IsActive = charge.IsActive
        };
    }

    public static LoanRateEntryDto MapToDto(this LoanRateEntry entry)
    {
        return new LoanRateEntryDto
        {
            Id = entry.Id,
            LoanId = entry.LoanId,
            EffectiveFrom = entry.EffectiveFrom,
            ReferenceRate = entry.ReferenceRate
        };
    }

    public static LoanInstallmentDto MapToDto(this LoanInstallment installment, IEnumerable<LoanCharge> charges, decimal outstandingBalance)
    {
        var chargesAmount = charges
            .Where(x => x.IsActive)
            .Where(x => IsChargeDueInMonth(x, installment.Year, installment.Month))
            .Sum(x => x.IsPercentageBased
                ? decimal.Round(outstandingBalance * x.Amount / 100m, 2, MidpointRounding.AwayFromZero)
                : x.Amount);

        return new LoanInstallmentDto
        {
            Id = installment.Id,
            LoanId = installment.LoanId,
            Year = installment.Year,
            Month = installment.Month,
            DueDate = installment.DueDate,
            Amount = installment.Amount + chargesAmount,
            PrincipalAmount = installment.PrincipalAmount,
            InterestAmount = installment.InterestAmount,
            IsPaid = installment.IsPaid,
            PaidAtUtc = installment.PaidAtUtc,
            ExpenseId = installment.Expense?.Id
        };
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

    private static LoanType ParseType(int value)
    {
        if (!Enum.IsDefined(typeof(LoanType), value))
        {
            return LoanType.Cash;
        }

        return (LoanType)value;
    }

    private static LoanInterestMode ParseInterestMode(int value)
    {
        if (!Enum.IsDefined(typeof(LoanInterestMode), value))
        {
            return LoanInterestMode.Fixed;
        }

        return (LoanInterestMode)value;
    }

    private static WiborPeriodType? ParseWiborPeriodType(int? value)
    {
        if (!value.HasValue || !Enum.IsDefined(typeof(WiborPeriodType), value.Value))
        {
            return null;
        }

        return (WiborPeriodType)value.Value;
    }

    private static LoanChargeType ParseChargeType(int value)
    {
        if (!Enum.IsDefined(typeof(LoanChargeType), value))
        {
            return LoanChargeType.Other;
        }

        return (LoanChargeType)value;
    }

    private static LoanChargeFrequencyType ParseFrequencyType(int value)
    {
        if (!Enum.IsDefined(typeof(LoanChargeFrequencyType), value))
        {
            return LoanChargeFrequencyType.Monthly;
        }

        return (LoanChargeFrequencyType)value;
    }
}