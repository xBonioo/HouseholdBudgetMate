using HouseholdBudgetMate.Web.Setup;

namespace HouseholdBudgetMate.Web.Middleware;

public sealed class AccessHardeningRedirectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IAccessHardeningService accessHardeningService)
    {
        if (ShouldBypass(context.Request))
        {
            await next(context);
            return;
        }

        if (await accessHardeningService.IsRequiredAsync(context.RequestAborted))
        {
            context.Response.Redirect("/access-setup", permanent: false);
            return;
        }

        await next(context);
    }

    private static bool ShouldBypass(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method))
        {
            return true;
        }

        var requestPath = request.Path;
        if (requestPath.StartsWithSegments("/setup", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWithSegments("/access-setup", StringComparison.OrdinalIgnoreCase)
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
