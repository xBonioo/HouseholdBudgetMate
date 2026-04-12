using System.ComponentModel.DataAnnotations;

namespace HouseholdBudgetMate.Abstractions.Enums;

public enum WiborPeriodType
{
    [Display(Name = "WIBOR 1M")]
    Wibor1M = 1,

    [Display(Name = "WIBOR 3M")]
    Wibor3M = 3,

    [Display(Name = "WIBOR 6M")]
    Wibor6M = 6,

    [Display(Name = "WIBOR 1R")]
    Wibor1R = 12
}