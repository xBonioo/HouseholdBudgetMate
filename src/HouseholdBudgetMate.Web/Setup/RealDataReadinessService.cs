using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Configurations;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HouseholdBudgetMate.Web.Setup;

public interface IRealDataReadinessService
{
    Task<RealDataReadinessReport> GetReportAsync(CancellationToken cancellationToken);
}

public sealed class RealDataReadinessService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IUserService userService,
    ApplicationConfiguration applicationConfiguration,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    ILoggerFactory loggerFactory) : IRealDataReadinessService
{
    public async Task<RealDataReadinessReport> GetReportAsync(CancellationToken cancellationToken)
    {
        var database = await ReadinessEndpoint.CheckDatabaseAsync(
            dbContextFactory,
            loggerFactory.CreateLogger<RealDataReadinessService>(),
            cancellationToken);
        var hasSecureAdmin = await userService.HasSecureInteractiveAdministratorAsync(cancellationToken);
        var publicFilesEnabled = RuntimeSafetyOptions.ShouldEnablePublicFileServing(configuration);
        var detailedErrorsEnabled = RuntimeSafetyOptions.ShouldEnableDetailedErrors(hostEnvironment, configuration);
        var logRetentionEnabled = applicationConfiguration.LogCleanupTask
                                  && applicationConfiguration.LogRetentionDays > 0;

        return new RealDataReadinessReport(
            [
                RealDataReadinessCheck.PassOrFail(
                    "Database readiness",
                    database.IsHealthy,
                    database.IsHealthy
                        ? "Application can connect to the configured database."
                        : "Application cannot connect to the configured database."),
                RealDataReadinessCheck.PassOrFail(
                    "Public /files",
                    !publicFilesEnabled,
                    publicFilesEnabled
                        ? "Public file serving is enabled; this is outside the real-data MVP boundary."
                        : "Public file serving is disabled for MVP mode."),
                RealDataReadinessCheck.PassOrFail(
                    "Production detailed errors",
                    !detailedErrorsEnabled,
                    detailedErrorsEnabled
                        ? "Detailed Blazor errors are enabled in this environment."
                        : "Detailed Blazor errors are disabled."),
                RealDataReadinessCheck.PassOrFail(
                    "Trusted session cookie hardening",
                    true,
                    "Remembered-session cookies use Strict SameSite and add Secure on HTTPS."),
                RealDataReadinessCheck.PassOrFail(
                    "Operational log retention",
                    logRetentionEnabled,
                    logRetentionEnabled
                        ? $"Operational Logs retention is enabled for {applicationConfiguration.LogRetentionDays} day(s)."
                        : "Operational Logs retention is disabled or misconfigured."),
                RealDataReadinessCheck.PassOrFail(
                    "Secure interactive administrator",
                    hasSecureAdmin,
                    hasSecureAdmin
                        ? "At least one non-technical administrator has a configured PIN."
                        : "No secure interactive administrator with PIN is configured.")
            ],
            [
                new RealDataReadinessManualItem(
                    "Accepted Free Render risk",
                    "Free Render remains an accepted MVP pilot risk, not durable production."),
                new RealDataReadinessManualItem(
                    "Manual pg_dump",
                    "Record the backup command, timestamp, operator, and output path in readiness-evidence.md."),
                new RealDataReadinessManualItem(
                    "Restore smoke test",
                    "Restore the dump into a non-production PostgreSQL database or mark the item pending with accepted risk."),
                new RealDataReadinessManualItem(
                    "Migration review",
                    "Record schema/data migration review, fresh backup status, and rollback or forward-fix notes.")
            ],
            "context/changes/secure-real-data-readiness/readiness-evidence.md");
    }
}

public sealed record RealDataReadinessReport(
    IReadOnlyList<RealDataReadinessCheck> AutomatedChecks,
    IReadOnlyList<RealDataReadinessManualItem> ManualItems,
    string EvidencePath)
{
    public bool IsAppCheckReady => AutomatedChecks.All(x => x.IsReady);
}

public sealed record RealDataReadinessCheck(
    string Name,
    bool IsReady,
    string Description)
{
    public static RealDataReadinessCheck PassOrFail(string name, bool isReady, string description)
        => new(name, isReady, description);
}

public sealed record RealDataReadinessManualItem(
    string Name,
    string Description);
