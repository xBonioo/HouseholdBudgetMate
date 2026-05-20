using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace HouseholdBudgetMate.Web;

internal sealed class StartupHostingOptions
{
    public required int HttpPort { get; init; }
    public required int HttpsPort { get; init; }
    public required string HttpsUrl { get; init; }
    public required bool EnableLanAccess { get; init; }
    public required bool OpenBrowserOnStartup { get; init; }
    public required X509Certificate2 HttpsCertificate { get; init; }
    public required IReadOnlyList<IPAddress> LanAddresses { get; init; }

    public static StartupHostingOptions Create(IConfiguration configuration, string appDataDirectory)
    {
        var preferredHttpPort = configuration.GetValue<int?>("WebHosting:HttpPort") ?? 5000;
        var preferredHttpsPort = configuration.GetValue<int?>("WebHosting:HttpsPort") ?? 5001;
        var enableLanAccess = configuration.GetValue<bool?>("WebHosting:EnableLanAccess") ?? false;
        var openBrowserOnStartup = configuration.GetValue<bool?>("WebHosting:OpenBrowserOnStartup") ?? false;
        var lanAddresses = enableLanAccess ? GetActivePrivateIpv4Addresses() : [];

        var httpPort = FindAvailablePort(preferredHttpPort, lanAddresses);
        var httpsPort = FindAvailablePort(preferredHttpsPort, lanAddresses, httpPort);
        var certPath = Path.Combine(appDataDirectory, "certs", "localhost.pfx");
        var certificate = LoadOrCreateCertificate(certPath, lanAddresses);
        TryTrustCertificate(certificate);

        return new StartupHostingOptions
        {
            HttpPort = httpPort,
            HttpsPort = httpsPort,
            HttpsUrl = $"https://localhost:{httpsPort}",
            EnableLanAccess = enableLanAccess,
            OpenBrowserOnStartup = openBrowserOnStartup,
            HttpsCertificate = certificate,
            LanAddresses = lanAddresses
        };
    }

    public void ConfigureKestrel(WebHostBuilderContext _, KestrelServerOptions kestrel)
    {
        kestrel.ListenLocalhost(HttpPort);
        kestrel.ListenLocalhost(HttpsPort, listen => listen.UseHttps(HttpsCertificate));

        if (!EnableLanAccess)
        {
            return;
        }

        foreach (var lanAddress in LanAddresses)
        {
            kestrel.Listen(lanAddress, HttpPort);
            kestrel.Listen(lanAddress, HttpsPort, listen => listen.UseHttps(HttpsCertificate));
        }
    }

    public IReadOnlyList<string> GetStartupUrls()
    {
        var urls = new List<string>
        {
            $"http://localhost:{HttpPort}",
            HttpsUrl
        };

        if (!EnableLanAccess)
        {
            return urls;
        }

        foreach (var lanAddress in LanAddresses)
        {
            urls.Add($"http://{lanAddress}:{HttpPort}");
            urls.Add($"https://{lanAddress}:{HttpsPort}");
        }

        return urls;
    }

    public void OpenBrowserIfEnabled(ILogger logger)
    {
        if (!OpenBrowserOnStartup || !Environment.UserInteractive)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = HttpsUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nie udalo sie automatycznie otworzyc przegladarki.");
        }
    }

    private static bool IsPortFree(int port, IReadOnlyCollection<IPAddress> lanAddresses)
    {
        var listeners = new List<TcpListener>();

        try
        {
            foreach (var address in new[] { IPAddress.Loopback }.Concat(lanAddresses))
            {
                var listener = new TcpListener(address, port);
                listener.Start();
                listeners.Add(listener);
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            foreach (var listener in listeners)
            {
                listener.Stop();
            }
        }
    }

    private static int FindAvailablePort(int preferredPort, IReadOnlyCollection<IPAddress> lanAddresses, params int[] blockedPorts)
    {
        if (!blockedPorts.Contains(preferredPort) && IsPortFree(preferredPort, lanAddresses))
        {
            return preferredPort;
        }

        for (var port = preferredPort + 1; port < preferredPort + 100; port++)
        {
            if (blockedPorts.Contains(port))
            {
                continue;
            }

            if (IsPortFree(port, lanAddresses))
            {
                return port;
            }
        }

        var fallbackListener = new TcpListener(IPAddress.Loopback, 0);
        fallbackListener.Start();
        var portNumber = ((IPEndPoint)fallbackListener.LocalEndpoint).Port;
        fallbackListener.Stop();
        return portNumber;
    }

    private static IReadOnlyList<IPAddress> GetActivePrivateIpv4Addresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up)
            .Where(x => x.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .SelectMany(x => x.GetIPProperties().UnicastAddresses)
            .Select(x => x.Address)
            .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
            .Where(x => !IPAddress.IsLoopback(x))
            .Where(IsPrivateIpv4Address)
            .Distinct()
            .OrderBy(x => x.ToString())
            .ToList();
    }

    private static bool IsPrivateIpv4Address(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        return bytes[0] == 10
               || bytes[0] == 192 && bytes[1] == 168
               || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;
    }

    private static X509Certificate2 LoadOrCreateCertificate(string certificatePath, IReadOnlyList<IPAddress> lanAddresses)
    {
        const string certificatePassword = "HouseholdBudgetMateLocalDev";
        const X509KeyStorageFlags keyStorageFlags = X509KeyStorageFlags.UserKeySet
                                                     | X509KeyStorageFlags.PersistKeySet
                                                     | X509KeyStorageFlags.Exportable;
        var certificateDirectory = Path.GetDirectoryName(certificatePath)
                                   ?? throw new InvalidOperationException("Nie mozna ustalic katalogu certyfikatu SSL.");

        Directory.CreateDirectory(certificateDirectory);

        if (File.Exists(certificatePath))
        {
            try
            {
                var existing = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, certificatePassword, keyStorageFlags);
                if (CertificateMatchesCurrentHostNames(existing, lanAddresses))
                {
                    return existing;
                }

                existing.Dispose();
            }
            catch
            {
                // Damaged or incompatible cert - regenerate.
            }
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
            false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName(Environment.MachineName);
        sanBuilder.AddDnsName($"{Environment.MachineName}.local");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
        foreach (var lanAddress in lanAddresses)
        {
            sanBuilder.AddIpAddress(lanAddress);
        }

        request.CertificateExtensions.Add(sanBuilder.Build());

        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(3));
        var pfxBytes = generated.Export(X509ContentType.Pfx, certificatePassword);
        File.WriteAllBytes(certificatePath, pfxBytes);

        return X509CertificateLoader.LoadPkcs12(pfxBytes, certificatePassword, keyStorageFlags);
    }

    private static bool CertificateMatchesCurrentHostNames(X509Certificate2 certificate, IReadOnlyList<IPAddress> lanAddresses)
    {
        var subjectAlternativeName = certificate.Extensions
            .FirstOrDefault(x => x.Oid?.Value == "2.5.29.17")
            ?.Format(multiLine: false);

        if (string.IsNullOrWhiteSpace(subjectAlternativeName)
            || !subjectAlternativeName.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return lanAddresses.All(address => subjectAlternativeName.Contains(address.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static void TryTrustCertificate(X509Certificate2 certificate)
    {
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            var existing = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                certificate.Thumbprint,
                validOnly: false);

            if (existing.Count == 0)
            {
                store.Add(certificate);
            }
        }
        catch
        {
            // If policy blocks trust-store writes, app still runs with HTTP/HTTPS;
            // browser may show cert warning.
        }
    }
}
