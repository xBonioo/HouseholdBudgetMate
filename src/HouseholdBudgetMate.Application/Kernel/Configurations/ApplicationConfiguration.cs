namespace HouseholdBudgetMate.Application.Kernel.Configurations;

public sealed class ApplicationConfiguration
{
    /// <summary>
    ///     Complete application name. E.g. SolutionName.ProjectName.
    /// </summary>
    /// <remarks>
    ///     <para>This name is used:</para>
    ///     <para>- as source of produced integrations events</para>
    ///     <para>- as app name in Swagger</para>
    /// </remarks>
    public string Name { get; init; } = null!;

    /// <summary>
    ///     Complete application name. E.g. SolutionName.ProjectName.
    /// </summary>
    /// <remarks>
    ///     <para>This name is used:</para>
    ///     <para>- as page title in MVC</para>
    /// </remarks>
    public string Title { get; init; } = null!;

    /// <summary>
    ///     Short application name. E.g. ProjectName.
    /// </summary>
    /// <remarks>
    ///     This service name is included in the insights and telemetry that is produced by the application.
    /// </remarks>
    public string ServiceName { get; init; } = null!;

    /// <summary>
    ///     Expose swagger endpoint.
    /// </summary>
    public bool UseSwagger { get; init; }

    /// <summary>
    ///     Application timezone. Used for calculating local time.
    /// </summary>
    public string Timezone { get; init; } = "Central European Standard Time";

    /// <summary>
    ///     Execute database migrations on application start.
    /// </summary>
    public bool MigrateDatabaseOnStart { get; init; }
    
    /// <summary>
    ///     Clean up database of old logs.
    /// </summary>
    public bool LogCleanupTask { get; init; }
    
    /// <summary>
    ///     Seed data to database.
    /// </summary>
    public bool SeedDataToDatabase { get; init; }
}