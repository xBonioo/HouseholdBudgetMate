using HouseholdBudgetMate.Application.Kernel.Configurations;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HouseholdBudgetMate.Application.Kernel.Logging;

public sealed class OperationalLogCleanupService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ApplicationConfiguration configuration,
    IDateTimeProvider dateTimeProvider,
    ILogger<OperationalLogCleanupService> logger)
{
    public async Task<OperationalLogCleanupResult> CleanupAsync(CancellationToken cancellationToken)
    {
        if (!configuration.LogCleanupTask)
        {
            return OperationalLogCleanupResult.Disabled();
        }

        if (configuration.LogRetentionDays <= 0)
        {
            logger.LogWarning("Operational log cleanup is enabled but LogRetentionDays is not positive.");
            return OperationalLogCleanupResult.Disabled();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            logger.LogWarning("Operational log cleanup skipped because the database is unavailable.");
            return OperationalLogCleanupResult.DatabaseUnavailable();
        }

        var cutoffUtc = dateTimeProvider.GetUtcDateTime().AddDays(-configuration.LogRetentionDays);
        var deleted = await DeleteOldOperationalLogsAsync(dbContext, cutoffUtc, cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation(
                "Deleted {DeletedCount} operational log rows older than {CutoffUtc}.",
                deleted,
                cutoffUtc);
        }

        return OperationalLogCleanupResult.Completed(deleted, cutoffUtc);
    }

    private static async Task<int> DeleteOldOperationalLogsAsync(
        ApplicationDbContext dbContext,
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Logs.Where(x => x.Timestamp < cutoffUtc);

        if (dbContext.Database.IsRelational())
        {
            return await query.ExecuteDeleteAsync(cancellationToken);
        }

        var logs = await query.ToListAsync(cancellationToken);
        if (logs.Count == 0)
        {
            return 0;
        }

        dbContext.Logs.RemoveRange(logs);
        await dbContext.SaveChangesAsync(cancellationToken);
        return logs.Count;
    }
}

public sealed class OperationalLogCleanupHostedService(
    IServiceProvider serviceProvider,
    ILogger<OperationalLogCleanupHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var cleanupService = scope.ServiceProvider.GetRequiredService<OperationalLogCleanupService>();
            await cleanupService.CleanupAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Operational log cleanup failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed record OperationalLogCleanupResult(
    bool IsEnabled,
    bool DatabaseAvailable,
    int DeletedCount,
    DateTime? CutoffUtc)
{
    public static OperationalLogCleanupResult Disabled()
        => new(false, true, 0, null);

    public static OperationalLogCleanupResult DatabaseUnavailable()
        => new(true, false, 0, null);

    public static OperationalLogCleanupResult Completed(int deletedCount, DateTime cutoffUtc)
        => new(true, true, deletedCount, cutoffUtc);
}
