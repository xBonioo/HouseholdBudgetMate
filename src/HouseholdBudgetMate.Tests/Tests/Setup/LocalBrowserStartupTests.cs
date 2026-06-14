using FluentAssertions;
using HouseholdBudgetMate.Web.Setup;

namespace HouseholdBudgetMate.Tests.Tests.Setup;

public sealed class LocalBrowserStartupTests
{
    [Fact]
    public void ResolveBrowserUrl_prefers_localhost_https_url()
    {
        var url = LocalBrowserStartup.ResolveBrowserUrl([
            "http://localhost:7134",
            "https://localhost:7135"
        ]);

        url.Should().Be("https://localhost:7135");
    }

    [Fact]
    public void ResolveBrowserUrl_falls_back_to_http_url()
    {
        var url = LocalBrowserStartup.ResolveBrowserUrl([
            "http://localhost:5000"
        ]);

        url.Should().Be("http://localhost:5000");
    }

    [Fact]
    public void ResolveBrowserUrl_converts_any_address_binding_to_localhost()
    {
        var url = LocalBrowserStartup.ResolveBrowserUrl([
            "http://0.0.0.0:5000"
        ]);

        url.Should().Be("http://localhost:5000");
    }
}
