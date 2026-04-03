using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class MonthPlanSeedService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider)
{
    public async Task EnsureCurrentMonthPlanAsync(CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.GetLocalDateTime();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await dbContext.MonthPlans
            .AnyAsync(x => x.Year == now.Year && x.Month == now.Month, cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.MonthPlans.Add(new MonthPlan
        {
            Year = now.Year,
            Month = now.Month,
            IsClosed = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

