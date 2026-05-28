using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Web.Setup;

public static class ReadinessEndpoint
{
    public const string Path = "/health/ready";

    public static IEndpointRouteBuilder MapReadinessEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, HandleAsync);
        return endpoints;
    }

    public static async Task<ReadinessDatabaseCheckResult> CheckDatabaseAsync(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return new ReadinessDatabaseCheckResult(canConnect);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Readiness database connectivity check failed");
            return new ReadinessDatabaseCheckResult(false);
        }
    }

    private static async Task<IResult> HandleAsync(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("HouseholdBudgetMate.Readiness");
        var database = await CheckDatabaseAsync(dbContextFactory, logger, cancellationToken);

        return database.IsHealthy
            ? Results.Ok(new { status = "healthy" })
            : Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

public sealed record ReadinessDatabaseCheckResult(bool IsHealthy);
