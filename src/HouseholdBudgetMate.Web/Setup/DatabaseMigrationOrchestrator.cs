using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using HouseholdBudgetMate.Migrations;

namespace HouseholdBudgetMate.Web.Setup;

public interface IDatabaseMigrationOrchestrator
{
    Task ValidateConnectionAndMigrateAsync(RuntimeDatabaseConfiguration runtimeDatabaseConfiguration, CancellationToken cancellationToken);
    Task MigrateConfiguredDatabaseAsync(CancellationToken cancellationToken);
}

public sealed class DatabaseMigrationOrchestrator(RuntimeConfigurationState runtimeConfigurationState) : IDatabaseMigrationOrchestrator
{
    public async Task ValidateConnectionAndMigrateAsync(RuntimeDatabaseConfiguration runtimeDatabaseConfiguration, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(runtimeDatabaseConfiguration.ToConnectionString());
        await connection.OpenAsync(cancellationToken);
        await connection.CloseAsync();

        await MigrateAsync(runtimeDatabaseConfiguration.ToConnectionString(), cancellationToken);
    }

    public async Task MigrateConfiguredDatabaseAsync(CancellationToken cancellationToken)
    {
        var runtimeDatabaseConfiguration = runtimeConfigurationState.GetDatabaseConfiguration();
        if (runtimeDatabaseConfiguration is null)
        {
            return;
        }

        await MigrateAsync(runtimeDatabaseConfiguration.ToConnectionString(), cancellationToken);
    }

    private static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning));
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("HouseholdBudgetMate.Migrations");
                npgsqlOptions.EnableRetryOnFailure(
                    5,
                    TimeSpan.FromSeconds(30),
                    null);
                npgsqlOptions.CommandTimeout(60);
            });

        await using var dbContext = new ApplicationDbContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}

