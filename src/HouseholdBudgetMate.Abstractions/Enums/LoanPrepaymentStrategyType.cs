using System.ComponentModel.DataAnnotations;

namespace HouseholdBudgetMate.Abstractions.Enums;

public enum LoanPrepaymentStrategyType
{
    [Display(Name = "Obniż ratę")]
    ReduceInstallment = 1,

    [Display(Name = "Skróć okres")]
    ShortenPeriod = 2
}