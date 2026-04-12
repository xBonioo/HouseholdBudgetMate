using System.ComponentModel.DataAnnotations;

namespace HouseholdBudgetMate.Abstractions.Enums;

public enum LoanChargeFrequencyType
{
    [Display(Name = "Jednorazowo")]
    OneTime = 1,

    [Display(Name = "Miesiecznie")]
    Monthly = 2,

    [Display(Name = "Rocznie")]
    Yearly = 3
}