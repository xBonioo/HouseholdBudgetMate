using System.ComponentModel.DataAnnotations;
using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Web.Setup;

public sealed class SetupInputModel
{
    [Required(ErrorMessage = "Host jest wymagany.")]
    public string Host { get; set; } = Environment.GetEnvironmentVariable("HOUSEHOLDBUDGETMATE_CONTAINER") == "true"
        ? "postgres"
        : "localhost";

    [Range(1, 65535, ErrorMessage = "Port musi być w zakresie 1-65535.")]
    public int Port { get; set; } = 5432;

    [Required(ErrorMessage = "Login jest wymagany.")]
    public string Username { get; set; } = Environment.GetEnvironmentVariable("HOUSEHOLDBUDGETMATE_CONTAINER") == "true"
        ? "household_budget_mate"
        : "postgres";

    [Required(ErrorMessage = "Hasło jest wymagane.")]
    public string Password { get; set; } = Environment.GetEnvironmentVariable("HOUSEHOLDBUDGETMATE_CONTAINER") == "true"
        ? "household_budget_mate"
        : string.Empty;

    [Required(ErrorMessage = "Nazwa bazy jest wymagana.")]
    public string Database { get; set; } = "household_budget_mate";

    public HouseholdMode HouseholdMode { get; set; } = HouseholdMode.SharedBudget;
    public string SharedWithUserIds { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nazwa pierwszego użytkownika jest wymagana.")]
    public string AppUsername { get; set; } = "Kamil";

    [RegularExpression(@"^\d{4,8}$", ErrorMessage = "PIN musi mieć od 4 do 8 cyfr.")]
    public string AppPin { get; set; } = string.Empty;
}
