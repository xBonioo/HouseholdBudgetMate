using System.ComponentModel.DataAnnotations;

namespace HouseholdBudgetMate.Web.Setup;

public sealed class SetupInputModel
{
    [Required(ErrorMessage = "Host jest wymagany.")]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535, ErrorMessage = "Port musi byc w zakresie 1-65535.")]
    public int Port { get; set; } = 5432;

    [Required(ErrorMessage = "Login jest wymagany.")]
    public string Username { get; set; } = "postgres";

    [Required(ErrorMessage = "Hasło jest wymagane.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nazwa bazy jest wymagana.")]
    public string Database { get; set; } = "household_budget_mate";
}
