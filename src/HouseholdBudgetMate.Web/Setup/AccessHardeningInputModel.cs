using System.ComponentModel.DataAnnotations;

namespace HouseholdBudgetMate.Web.Setup;

public sealed class AccessHardeningInputModel
{
    [Required(ErrorMessage = "Nazwa administratora jest wymagana.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Nazwa administratora musi mieć od 3 do 100 znaków.")]
    public string Username { get; set; } = "Admin";

    [RegularExpression(@"^\d{4,8}$", ErrorMessage = "PIN musi mieć od 4 do 8 cyfr.")]
    public string Pin { get; set; } = string.Empty;
}
