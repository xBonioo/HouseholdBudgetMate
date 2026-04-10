using Npgsql;

namespace HouseholdBudgetMate.Web.Setup;

public sealed class RuntimeDatabaseConfiguration
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 5432;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Database { get; init; } = string.Empty;

    public string ToConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Username = Username,
            Password = Password,
            Database = Database,
            Timeout = 5,
            CommandTimeout = 30
        };

        return builder.ConnectionString;
    }
}
