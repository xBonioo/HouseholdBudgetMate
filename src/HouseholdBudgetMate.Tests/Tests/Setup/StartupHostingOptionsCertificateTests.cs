using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using HouseholdBudgetMate.Web.Setup;

namespace HouseholdBudgetMate.Tests.Tests.Setup;

public sealed class StartupHostingOptionsCertificateTests
{
    [Fact]
    public void CertificateCanBeReusedForLocalhost_Should_Not_Require_Current_Lan_Address_In_SubjectAlternativeName()
    {
        using var certificate = CreateCertificate(["localhost"]);

        var canBeReused = InvokeCertificateCanBeReusedForLocalhost(certificate);

        canBeReused.Should().BeTrue();
    }

    [Fact]
    public void CertificateCanBeReusedForLocalhost_Should_Reject_Certificate_Without_Localhost_SubjectAlternativeName()
    {
        using var certificate = CreateCertificate(["household-budget-mate.local"]);

        var canBeReused = InvokeCertificateCanBeReusedForLocalhost(certificate);

        canBeReused.Should().BeFalse();
    }

    private static bool InvokeCertificateCanBeReusedForLocalhost(X509Certificate2 certificate)
    {
        var startupHostingOptionsType = typeof(LocalBrowserStartup).Assembly
            .GetType("HouseholdBudgetMate.Web.StartupHostingOptions", throwOnError: true)!;
        var method = startupHostingOptionsType.GetMethod(
            "CertificateCanBeReusedForLocalhost",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        return (bool)method.Invoke(null, [certificate])!;
    }

    private static X509Certificate2 CreateCertificate(IReadOnlyList<string> dnsNames)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
            false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        foreach (var dnsName in dnsNames)
        {
            sanBuilder.AddDnsName(dnsName);
        }

        sanBuilder.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(3));
    }
}
