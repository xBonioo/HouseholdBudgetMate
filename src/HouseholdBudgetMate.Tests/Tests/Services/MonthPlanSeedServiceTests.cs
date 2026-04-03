using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class MonthPlanSeedServiceTests
{
    [Fact]
    public async Task EnsureCurrentMonthPlanAsync_Should_Be_Idempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var now = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);

        var service = new MonthPlanSeedService(
            TestDbContextFactory.CreateFactory(dbName),
            new StaticDateTimeProvider(now));

        await service.EnsureCurrentMonthPlanAsync(CancellationToken.None);
        await service.EnsureCurrentMonthPlanAsync(CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(dbName);
        var plans = await verifyContext.MonthPlans.ToListAsync();

        Assert.Single(plans);
        Assert.Equal(2026, plans[0].Year);
        Assert.Equal(4, plans[0].Month);
    }
}


