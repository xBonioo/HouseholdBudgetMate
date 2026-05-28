using FluentAssertions;
using HouseholdBudgetMate.Migrations;
using HouseholdBudgetMate.Tests.Shared;
using HouseholdBudgetMate.Web.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HouseholdBudgetMate.Tests.Tests.Setup;

public sealed class ReadinessHealthTests
{
    [Fact]
    public async Task CheckDatabaseAsync_Should_Report_Healthy_When_Database_Can_Connect()
    {
        var result = await ReadinessEndpoint.CheckDatabaseAsync(
            TestDbContextFactory.CreateFactory(),
            NullLogger.Instance,
            CancellationToken.None);

        result.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task CheckDatabaseAsync_Should_Report_Unhealthy_Without_Leaking_Exception()
    {
        var result = await ReadinessEndpoint.CheckDatabaseAsync(
            new ThrowingDbContextFactory(),
            NullLogger.Instance,
            CancellationToken.None);

        result.IsHealthy.Should().BeFalse();
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            throw new InvalidOperationException("sensitive connection failure");
        }

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("sensitive connection failure");
        }
    }
}
