using System.Net;
using FluentAssertions;
using HouseholdBudgetMate.Web.Middleware;
using HouseholdBudgetMate.Web.Setup;
using Microsoft.AspNetCore.Http;
using Moq;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class AccessHardeningRedirectMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Should_Deny_Remote_Request_When_Hardening_Is_Required()
    {
        var context = CreateContext("/", IPAddress.Parse("203.0.113.10"));
        var hardening = new Mock<IAccessHardeningService>();
        hardening.Setup(x => x.IsRequiredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var recovery = new Mock<IAccessRecoveryService>();
        var middleware = new AccessHardeningRedirectMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(
            context,
            hardening.Object,
            recovery.Object,
            new LocalAccessGrantService());

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.Headers.Location.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_Should_Redirect_Local_Request_With_A_Hardening_Grant()
    {
        var context = CreateContext("/", IPAddress.Loopback);
        var hardening = new Mock<IAccessHardeningService>();
        hardening.Setup(x => x.IsRequiredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var recovery = new Mock<IAccessRecoveryService>();
        var grants = new LocalAccessGrantService();
        var middleware = new AccessHardeningRedirectMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, hardening.Object, recovery.Object, grants);

        context.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        var location = context.Response.Headers.Location.Single();
        location.Should().StartWith("/access-setup?grant=");
        var grant = Uri.UnescapeDataString(location[(location.IndexOf('=') + 1)..]);
        grants.IsValid(grant, LocalAccessPurposes.AccessHardening).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Should_Use_Direct_Address_Instead_Of_Spoofed_Forwarded_Loopback()
    {
        var context = CreateContext("/access-recovery", IPAddress.Parse("203.0.113.10"));
        LocalAccessGrantService.CaptureDirectRemoteAddress(context);
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        var middleware = new AccessHardeningRedirectMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(
            context,
            Mock.Of<IAccessHardeningService>(),
            Mock.Of<IAccessRecoveryService>(),
            new LocalAccessGrantService());

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private static DefaultHttpContext CreateContext(string path, IPAddress remoteAddress)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = remoteAddress;
        return context;
    }
}
