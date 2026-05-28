using HouseholdBudgetMate.Web.Setup;

namespace HouseholdBudgetMate.Web.Middleware;

public sealed class SetupRedirectMiddleware(RequestDelegate next, RuntimeConfigurationState runtimeConfigurationState)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (runtimeConfigurationState.IsConfigured)
        {
            await next(context);
            return;
        }

        var requestPath = context.Request.Path;
        if (IsAllowedPath(requestPath))
        {
            await next(context);
            return;
        }

        context.Response.Redirect("/setup", permanent: false);
    }

    private static bool IsAllowedPath(PathString requestPath)
    {
        if (requestPath.StartsWithSegments("/setup", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWithSegments("/_content", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Path.HasExtension(requestPath.Value);
    }
}

