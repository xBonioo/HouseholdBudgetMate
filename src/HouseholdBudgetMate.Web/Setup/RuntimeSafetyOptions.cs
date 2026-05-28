using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HouseholdBudgetMate.Web.Setup;

public static class RuntimeSafetyOptions
{
    public const string DetailedErrorsConfigurationKey = "Blazor:DetailedErrors";
    public const string PublicFileServingConfigurationKey = "FileStorage:EnablePublicFileServing";

    public static bool ShouldEnableDetailedErrors(IHostEnvironment environment, IConfiguration configuration)
    {
        return environment.IsDevelopment()
               || configuration.GetValue<bool>(DetailedErrorsConfigurationKey);
    }

    public static bool ShouldEnablePublicFileServing(IConfiguration configuration)
    {
        return configuration.GetValue<bool>(PublicFileServingConfigurationKey);
    }
}
