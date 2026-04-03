using System.ComponentModel.DataAnnotations;

namespace HouseholdBudgetMate.Abstractions.Enums;

public enum FileContextType
{
    [Display(Name = "Przykład")] 
    Example,


    [Display(Name = "Nieznane")] 
    Unknown = 99
}