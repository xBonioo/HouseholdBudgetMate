using System.Globalization;
using HouseholdBudgetMate.Application.Kernel.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HouseholdBudgetMate.Application.Kernel.Configurations;
using NpgsqlTypes;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

namespace HouseholdBudgetMate.Application.Kernel.Extensions;

public static class SerilogExtensions
{
    public static readonly int SlowRequestThresholdMs = 200;
    public static readonly int SlowDbCommandThresholdMs = 200;
    private static string _applicationType = "Unknown";

    public static void AddSerilogLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        var connectionString = PostgreSqlConnectionStringResolver.Resolve(builder.Configuration);

        _applicationType = builder.Configuration["Serilog:Properties:ApplicationType"] ?? "Unknown";

        var loggerConfiguration = new LoggerConfiguration()
            .Filter.ByIncludingOnly(ShouldIncludeLogEvent)
            .Enrich.WithProperty("ApplicationType", _applicationType)
            .Enrich.FromLogContext();

        try
        {
            // Some first-run environments do not have full Serilog sink config ready yet.
            loggerConfiguration = loggerConfiguration.ReadFrom.Configuration(builder.Configuration);
        }
        catch (InvalidOperationException)
        {
            // Fallback to minimal logger configuration so app can continue and reach /setup.
        }

        // During first-run setup there is no DB connection yet, so keep logger alive without PostgreSQL sink.
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            loggerConfiguration = loggerConfiguration.WriteTo.PostgreSQL(
                connectionString: connectionString,
                tableName: "Logs",
                columnOptions: BuildColumnWriters(),
                needAutoCreateTable: false);
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        builder.Host.UseSerilog();
    }

    public static IServiceCollection AddOperationalLogCleanup(this IServiceCollection services)
    {
        services.AddScoped<OperationalLogCleanupService>();
        services.AddHostedService<OperationalLogCleanupHostedService>();
        return services;
    }

    public static void UseSerilogRequestLoggingWithThreshold(this WebApplication app)
    {
        var requestThresholdMs = app.Configuration.GetValue<int?>("Serilog:Thresholds:RequestMs")
                                 ?? SlowRequestThresholdMs;

        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (httpContext.Request.Path.StartsWithSegments("/_blazor"))
                    return LogEventLevel.Debug;
                
                if (ex != null || httpContext.Response.StatusCode >= 500)
                    return LogEventLevel.Error;

                return elapsed >= requestThresholdMs
                    ? LogEventLevel.Information
                    : LogEventLevel.Debug;
            };
        });
    }

    private static IDictionary<string, ColumnWriterBase> BuildColumnWriters()
        => new Dictionary<string, ColumnWriterBase>
        {
            { "message", new RenderedMessageColumnWriter() },
            { "message_template", new MessageTemplateColumnWriter() },
            { "level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
            { "timestamp", new TimestampColumnWriter() },
            { "exception", new ExceptionColumnWriter() },
            { "properties", new LogEventSerializedColumnWriter() }
        };

    private static bool ShouldIncludeLogEvent(LogEvent logEvent)
    {
        if (IsEfCommandLog(logEvent))
        {
            if (logEvent.Level >= LogEventLevel.Warning || logEvent.Exception != null)
                return true;

            return TryGetElapsedMs(logEvent, out var elapsedMs) && elapsedMs >= SlowDbCommandThresholdMs;
        }

        return true;
    }

    private static bool IsEfCommandLog(LogEvent logEvent)
        => logEvent.Properties.TryGetValue("SourceContext", out var sourceContext)
           && sourceContext is ScalarValue { Value: string and "Microsoft.EntityFrameworkCore.Database.Command" };

    private static bool TryGetElapsedMs(LogEvent logEvent, out double elapsedMs)
        => TryGetScalarNumber(logEvent, "elapsed", out elapsedMs)
           || TryGetScalarNumber(logEvent, "Elapsed", out elapsedMs)
           || TryGetScalarNumber(logEvent, "elapsedMilliseconds", out elapsedMs)
           || TryGetScalarNumber(logEvent, "ElapsedMilliseconds", out elapsedMs);

    private static bool TryGetScalarNumber(LogEvent logEvent, string propertyName, out double value)
    {
        value = 0;
        if (!logEvent.Properties.TryGetValue(propertyName, out var prop))
            return false;

        switch (prop)
        {
            case ScalarValue { Value: int i }:
                value = i; return true;
            case ScalarValue { Value: long l }:
                value = l; return true;
            case ScalarValue { Value: double d }:
                value = d; return true;
            case ScalarValue { Value: float f }:
                value = f; return true;
            case ScalarValue { Value: decimal m }:
                value = (double)m; return true;
            case ScalarValue { Value: string s }
                when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }
}
