using System.ComponentModel.DataAnnotations;

namespace HouseholdBudgetMate.Abstractions.Enums;

public enum LoanType
{
    [Display(Name = "Gotówkowy")]
    Cash = 1,

    [Display(Name = "Hipoteczny")]
    Mortgage = 2
}