using System.Diagnostics;

namespace HouseholdBudgetMate.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private Process? _webProcess;
    private readonly string _installDirectory;
    private readonly string _webExePath;
    private readonly string _appUrl = "https://localhost:5001";

    public TrayApplicationContext()
    {
        _installDirectory = ResolveInstallDirectory();
        _webExePath = Path.Combine(_installDirectory, "HouseholdBudgetMate.Web.exe");

        StartWebProcess();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Otworz aplikacje", null, (_, _) => OpenBrowser());
        menu.Items.Add("Uruchom ponownie backend", null, (_, _) => RestartWebProcess());
        menu.Items.Add("Zamknij", null, (_, _) => Exit());

        _notifyIcon = new NotifyIcon
        {
            Text = "HouseholdBudgetMate",
            Icon = ResolveTrayIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenBrowser();
    }

    private static string ResolveInstallDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var processDir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(processDir))
            {
                return processDir;
            }
        }

        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            return baseDir;
        }

        return Environment.CurrentDirectory;
    }

    private static Icon ResolveTrayIcon()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                var icon = Icon.ExtractAssociatedIcon(processPath);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch
        {
            // fallback below
        }

        return SystemIcons.Application;
    }

    private void StartWebProcess()
    {
        if (!File.Exists(_webExePath))
        {
            MessageBox.Show($"Brak pliku: {_webExePath}", "HouseholdBudgetMate", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Exit();
            return;
        }

        _webProcess = Process.Start(new ProcessStartInfo
        {
            FileName = _webExePath,
            WorkingDirectory = _installDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private void RestartWebProcess()
    {
        StopWebProcess();
        StartWebProcess();
    }

    private void StopWebProcess()
    {
        try
        {
            if (_webProcess is { HasExited: false })
            {
                _webProcess.Kill(entireProcessTree: true);
                _webProcess.WaitForExit(5000);
            }
        }
        catch
        {
            // best effort
        }
        finally
        {
            _webProcess?.Dispose();
            _webProcess = null;
        }
    }

    private void OpenBrowser()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _appUrl,
            UseShellExecute = true
        });
    }

    private void Exit()
    {
        _notifyIcon.Visible = false;
        StopWebProcess();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
