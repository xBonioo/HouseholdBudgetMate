using System.Diagnostics;
using System.Net;
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
    public required bool OpenBrowserOnStartup { get; init; }
    public required X509Certificate2 HttpsCertificate { get; init; }

    public static StartupHostingOptions Create(IConfiguration configuration, string appDataDirectory)
    {
        var preferredHttpPort = configuration.GetValue<int?>("WebHosting:HttpPort") ?? 5000;
        var preferredHttpsPort = configuration.GetValue<int?>("WebHosting:HttpsPort") ?? 5001;
        var openBrowserOnStartup = configuration.GetValue<bool?>("WebHosting:OpenBrowserOnStartup") ?? true;

        var httpPort = FindAvailablePort(preferredHttpPort);
        var httpsPort = FindAvailablePort(preferredHttpsPort, httpPort);
        var certPath = Path.Combine(appDataDirectory, "certs", "localhost.pfx");
        var certificate = LoadOrCreateCertificate(certPath);
        TryTrustCertificate(certificate);
        

        return new StartupHostingOptions
        {
            HttpPort = httpPort,
            HttpsPort = httpsPort,
            HttpsUrl = $"https://localhost:{httpsPort}",
            OpenBrowserOnStartup = openBrowserOnStartup,
            HttpsCertificate = certificate
        };
    }

    public void ConfigureKestrel(WebHostBuilderContext _, KestrelServerOptions kestrel)
    {
        kestrel.ListenLocalhost(HttpPort);
        kestrel.ListenLocalhost(HttpsPort, listen => listen.UseHttps(HttpsCertificate));
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

    private static bool IsPortFree(int port)
    {
        TcpListener? listener = null;

        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }

    private static int FindAvailablePort(int preferredPort, params int[] blockedPorts)
    {
        if (!blockedPorts.Contains(preferredPort) && IsPortFree(preferredPort))
        {
            return preferredPort;
        }

        for (var port = preferredPort + 1; port < preferredPort + 100; port++)
        {
            if (blockedPorts.Contains(port))
            {
                continue;
            }

            if (IsPortFree(port))
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

    private static X509Certificate2 LoadOrCreateCertificate(string certificatePath)
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
                return X509CertificateLoader.LoadPkcs12FromFile(certificatePath, certificatePassword, keyStorageFlags);
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
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(3));
        var pfxBytes = generated.Export(X509ContentType.Pfx, certificatePassword);
        File.WriteAllBytes(certificatePath, pfxBytes);

        return X509CertificateLoader.LoadPkcs12(pfxBytes, certificatePassword, keyStorageFlags);
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
