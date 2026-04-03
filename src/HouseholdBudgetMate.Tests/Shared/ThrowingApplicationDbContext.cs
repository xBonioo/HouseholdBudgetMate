using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Shared;

// Local derived context used for tests to force SaveChangesAsync to throw
public class ThrowingApplicationDbContext(DbContextOptions<Migrations.ApplicationDbContext> options, bool throwOnSave)
    : Migrations.ApplicationDbContext(options)
{
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (throwOnSave)
            throw new Exception("Simulated SaveChanges failure");

        return base.SaveChangesAsync(cancellationToken);
    }
}