using System.ComponentModel.DataAnnotations;

namespace HouseholdBudgetMate.Abstractions.Enums;

public enum AccountType
{
    [Display(Name = "Gotówka")]
    Cash = 1,

    [Display(Name = "Bank")]
    Bank = 2,

    [Display(Name = "Oszczędność")]
    Savings = 3,
    
    [Display(Name = "Inne")]
    Other = 99
}

