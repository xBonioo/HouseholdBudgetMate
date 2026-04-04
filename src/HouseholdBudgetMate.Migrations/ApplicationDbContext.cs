using Microsoft.EntityFrameworkCore;
using HouseholdBudgetMate.Domain;
using HouseholdBudgetMate.Domain.EntityConfiguration;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Migrations;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<LogEntry> Logs { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<MonthPlan> MonthPlans { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<ExpenseLineItem> ExpenseLineItems { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountMonthBalance> AccountMonthBalances { get; set; }
    public DbSet<Income> Incomes { get; set; }
    public DbSet<RegularIncomeDefinition> RegularIncomeDefinitions { get; set; }
    public DbSet<MonthSavingsTransferItem> MonthSavingsTransferItems { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DomainAssemblyMarker).Assembly);
    }
    
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ITimestampable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.UpdatedAtUtc = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                case EntityState.Deleted:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}