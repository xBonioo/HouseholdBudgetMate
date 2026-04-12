using System.ComponentModel.DataAnnotations;

namespace HouseholdBudgetMate.Abstractions.Enums;

public enum LoanChargeType
{
    [Display(Name = "Ubezpieczenie")]
    Insurance = 1,

    [Display(Name = "Prowizja")]
    Commission = 2,

    [Display(Name = "Oplata")]
    Fee = 3,

    [Display(Name = "Inne")]
    Other = 4
}