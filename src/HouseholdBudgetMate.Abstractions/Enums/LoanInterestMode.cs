using System.ComponentModel.DataAnnotations;

namespace HouseholdBudgetMate.Abstractions.Enums;

public enum LoanInterestMode
{
    [Display(Name = "Stałe")]
    Fixed = 1,

    [Display(Name = "Zmiennie (WIBOR)")]
    VariableWibor = 2
}