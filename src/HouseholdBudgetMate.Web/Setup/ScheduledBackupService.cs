using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Timing;

namespace HouseholdBudgetMate.Web.Setup;

public sealed class ScheduledBackupService(
    IServiceProvider serviceProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<ScheduledBackupService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var settingsStore = scope.ServiceProvider.GetRequiredService<IBackupSettingsStore>();
            var settings = await settingsStore.GetAsync(cancellationToken);

            if (!BackupScheduleCalculator.IsDue(settings, dateTimeProvider.GetLocalDateTimeOffset()))
            {
                return;
            }

            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            await backupService.RunScheduledBackupNowAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Scheduled backup failed.");
        }
    }
}
