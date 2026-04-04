using HouseholdBudgetMate.Application.Seeds;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class MonthPlanSeedServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private MonthPlanSeedService CreateService()
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(DateTime.UtcNow);
        return new MonthPlanSeedService(factory, provider);
    }

    [Fact]
    public async Task EnsureCurrentMonthPlanAsync_Should_Be_Idempotent()
    {
        var service = CreateService();

        await service.EnsureCurrentMonthPlanAsync(CancellationToken.None);
        await service.EnsureCurrentMonthPlanAsync(CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var plans = await verifyContext.MonthPlans.ToListAsync();

        Assert.Single(plans);
        Assert.Equal(2026, plans[0].Year);
        Assert.Equal(4, plans[0].Month);
    }
}
