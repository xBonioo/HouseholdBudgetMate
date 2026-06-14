using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HouseholdBudgetMate.Web.Setup;

public static class LocalBrowserStartup
{
    public const string OpenBrowserOnStartupConfigurationKey = "WebHosting:OpenBrowserOnStartup";

    public static bool ShouldOpenBrowser(
        IHostEnvironment environment,
        IConfiguration configuration,
        bool isPublishedExecutable,
        bool isContainerOrCloud)
    {
        return environment.IsDevelopment()
               && !isPublishedExecutable
               && !isContainerOrCloud
               && (configuration.GetValue<bool?>(OpenBrowserOnStartupConfigurationKey) ?? false);
    }

    public static string? ResolveBrowserUrl(IEnumerable<string> urls)
    {
        return urls
            .Select(NormalizeLocalhostUrl)
            .Where(url => url is not null)
            .OrderByDescending(url => url!.StartsWith("https://localhost", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(url => url!.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    public static void OpenBrowserIfEnabled(bool enabled, IEnumerable<string> urls, ILogger logger)
    {
        if (!enabled || !Environment.UserInteractive)
        {
            return;
        }

        var browserUrl = ResolveBrowserUrl(urls);
        if (string.IsNullOrWhiteSpace(browserUrl))
        {
            logger.LogWarning("Nie udalo sie ustalic lokalnego adresu aplikacji do otwarcia w przegladarce.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = browserUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nie udalo sie automatycznie otworzyc przegladarki.");
        }
    }

    private static string? NormalizeLocalhostUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var host = uri.Host is "0.0.0.0" or "::" or "[::]" or "+"
            ? "localhost"
            : uri.Host;

        var builder = new UriBuilder(uri)
        {
            Host = host
        };

        return builder.Uri.ToString().TrimEnd('/');
    }
}
