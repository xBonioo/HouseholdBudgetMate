using HouseholdBudgetMate.Web.Setup;

namespace HouseholdBudgetMate.Web.Middleware;

public sealed class AccessHardeningRedirectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IAccessHardeningService accessHardeningService,
        IAccessRecoveryService accessRecoveryService,
        ILocalAccessGrantService localAccessGrantService)
    {
        var localFlowPurpose = GetLocalFlowPurpose(context.Request.Path);
        if (localFlowPurpose is not null && HttpMethods.IsGet(context.Request.Method))
        {
            if (!LocalAccessGrantService.IsLoopbackRequest(context))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var grant = context.Request.Query[LocalAccessGrantService.QueryParameterName].FirstOrDefault();
            if (!localAccessGrantService.IsValid(grant, localFlowPurpose))
            {
                RedirectToLocalFlow(context, localAccessGrantService, context.Request.Path, localFlowPurpose);
                return;
            }

            await next(context);
            return;
        }

        if (ShouldBypass(context.Request))
        {
            await next(context);
            return;
        }

        if (accessRecoveryService.IsRecoveryRequired)
        {
            RedirectToLocalFlow(context, localAccessGrantService, "/access-recovery", LocalAccessPurposes.AccessRecovery);
            return;
        }

        if (await accessHardeningService.IsRequiredAsync(context.RequestAborted))
        {
            RedirectToLocalFlow(context, localAccessGrantService, "/access-setup", LocalAccessPurposes.AccessHardening);
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
            || requestPath.StartsWithSegments("/access-recovery", StringComparison.OrdinalIgnoreCase)
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

    private static string? GetLocalFlowPurpose(PathString requestPath)
    {
        if (requestPath.StartsWithSegments("/access-setup", StringComparison.OrdinalIgnoreCase))
        {
            return LocalAccessPurposes.AccessHardening;
        }

        return requestPath.StartsWithSegments("/access-recovery", StringComparison.OrdinalIgnoreCase)
            ? LocalAccessPurposes.AccessRecovery
            : null;
    }

    private static void RedirectToLocalFlow(
        HttpContext context,
        ILocalAccessGrantService localAccessGrantService,
        PathString path,
        string purpose)
    {
        var grant = localAccessGrantService.IssueGrantForRequest(context, purpose);
        if (grant is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var location = $"{path}?{LocalAccessGrantService.QueryParameterName}={Uri.EscapeDataString(grant)}";
        context.Response.Redirect(location, permanent: false);
    }
}
