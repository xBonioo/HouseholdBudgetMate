using System.Data.Common;
using Microsoft.Extensions.Configuration;

namespace HouseholdBudgetMate.Application.Kernel.Configurations;

public static class PostgreSqlConnectionStringResolver
{
    private static readonly string[] DatabaseUrlKeys =
    [
        "DATABASE_URL",
        "POSTGRES_URL",
        "POSTGRESQL_URL",
        "DATABASE_CONNECTION_STRING"
    ];

    public static string? Resolve(IConfiguration configuration)
    {
        var configuredConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        foreach (var key in DatabaseUrlKeys)
        {
            var databaseUrl = configuration[key];
            var connectionString = ConvertDatabaseUrl(databaseUrl);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }
        }

        return null;
    }

    public static string? ConvertDatabaseUrl(string? databaseUrl)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("postgres" or "postgresql"))
        {
            return databaseUrl;
        }

        var userInfoParts = uri.UserInfo.Split(':', 2);
        var builder = new DbConnectionStringBuilder
        {
            ["Host"] = uri.Host,
            ["Port"] = uri.Port > 0 ? uri.Port : 5432,
            ["Database"] = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            ["Username"] = Uri.UnescapeDataString(userInfoParts.ElementAtOrDefault(0) ?? string.Empty),
            ["Password"] = Uri.UnescapeDataString(userInfoParts.ElementAtOrDefault(1) ?? string.Empty),
            ["Timeout"] = 5,
            ["Command Timeout"] = 30
        };

        ApplyQueryOptions(uri.Query, builder);

        return builder.ConnectionString;
    }

    private static void ApplyQueryOptions(string query, DbConnectionStringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var parameters = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .ToDictionary(
                x => Uri.UnescapeDataString(x[0]),
                x => Uri.UnescapeDataString(x[1]),
                StringComparer.OrdinalIgnoreCase);

        if (parameters.TryGetValue("sslmode", out var sslMode))
        {
            builder["Ssl Mode"] = sslMode;
        }
        else if (parameters.TryGetValue("ssl", out var ssl)
                 && bool.TryParse(ssl, out var sslEnabled)
                 && sslEnabled)
        {
            builder["Ssl Mode"] = "Require";
        }

        if (parameters.TryGetValue("trustServerCertificate", out var trustServerCertificate))
        {
            builder["Trust Server Certificate"] = trustServerCertificate;
        }
    }
}
