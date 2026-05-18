using HouseholdBudgetMate.Domain;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HouseholdBudgetMate.Migrations;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    CurrentUserContext? currentUserContext = null) : DbContext(options)
{
    public string CurrentUserId => currentUserContext?.UserId ?? User.DefaultUserId;
    public string CurrentBudgetOwnerUserId => currentUserContext?.BudgetOwnerUserId ?? CurrentUserId;

    public DbSet<User> Users { get; set; }
    public DbSet<LogEntry> Logs { get; set; }

    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }

    public DbSet<MonthPlan> MonthPlans { get; set; }
    public DbSet<MonthSavingsTransferItem> MonthSavingsTransferItems { get; set; }

    public DbSet<Expense> Expenses { get; set; }
    public DbSet<ExpenseLineItem> ExpenseLineItems { get; set; }
    public DbSet<RegularExpenseDefinition> RegularExpenseDefinitions { get; set; }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountMonthBalance> AccountMonthBalances { get; set; }
    public DbSet<Income> Incomes { get; set; }
    public DbSet<RegularIncomeDefinition> RegularIncomeDefinitions { get; set; }

    public DbSet<Loan> Loans { get; set; }
    public DbSet<LoanInstallment> LoanInstallments { get; set; }
    public DbSet<LoanRateEntry> LoanRateEntries { get; set; }
    public DbSet<LoanCharge> LoanCharges { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DomainAssemblyMarker).Assembly);
        ConfigureUserScopedEntities(modelBuilder);
        ConfigureUserScopedFilters(modelBuilder);
    }

    public override int SaveChanges()
    {
        UpdateTimestampsAndUserScope();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestampsAndUserScope();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestampsAndUserScope()
    {
        var now = DateTime.UtcNow;

        StampUserScope(ChangeTracker.Entries<Account>(), (entity, userId) => entity.UserId = userId);
        StampUserScope(ChangeTracker.Entries<AccountMonthBalance>(), (entity, userId) => entity.UserId = userId);
        StampUserScope(ChangeTracker.Entries<Expense>(), (entity, userId) => entity.UserId = userId);
        StampUserScope(ChangeTracker.Entries<ExpenseLineItem>(), (entity, userId) => entity.UserId = userId);
        StampUserScope(ChangeTracker.Entries<Income>(), (entity, userId) => entity.UserId = userId);
        StampUserScope(ChangeTracker.Entries<Loan>(), (entity, userId) => entity.UserId = userId);
        StampUserScope(ChangeTracker.Entries<MonthPlan>(), (entity, userId) => entity.UserId = userId);
        StampUserScope(ChangeTracker.Entries<MonthSavingsTransferItem>(), (entity, userId) => entity.UserId = userId);
        StampUserScope(ChangeTracker.Entries<RegularExpenseDefinition>(), (entity, userId) => entity.UserId = userId);
        StampUserScope(ChangeTracker.Entries<RegularIncomeDefinition>(), (entity, userId) => entity.UserId = userId);

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

    private void StampUserScope<TEntity>(
        IEnumerable<EntityEntry<TEntity>> entries,
        Action<TEntity, string> setUserId)
        where TEntity : class
    {
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                setUserId(entry.Entity, CurrentBudgetOwnerUserId);
            }
        }
    }

    private static void ConfigureUserScopedEntities(ModelBuilder modelBuilder)
    {
        ConfigureUserScopedEntity<Account>(modelBuilder);
        ConfigureUserScopedEntity<AccountMonthBalance>(modelBuilder);
        ConfigureUserScopedEntity<Expense>(modelBuilder);
        ConfigureUserScopedEntity<ExpenseLineItem>(modelBuilder);
        ConfigureUserScopedEntity<Income>(modelBuilder);
        ConfigureUserScopedEntity<Loan>(modelBuilder);
        ConfigureUserScopedEntity<MonthPlan>(modelBuilder);
        ConfigureUserScopedEntity<MonthSavingsTransferItem>(modelBuilder);
        ConfigureUserScopedEntity<RegularExpenseDefinition>(modelBuilder);
        ConfigureUserScopedEntity<RegularIncomeDefinition>(modelBuilder);
    }

    private static void ConfigureUserScopedEntity<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>(builder =>
        {
            builder.Property<string>("UserId")
                .HasMaxLength(128)
                .IsRequired();

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex("UserId");
        });
    }

    private void ConfigureUserScopedFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>().HasQueryFilter(x => x.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<AccountMonthBalance>().HasQueryFilter(x => x.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<Category>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Expense>().HasQueryFilter(x => x.UserId == CurrentBudgetOwnerUserId && !x.IsDeleted);
        modelBuilder.Entity<ExpenseLineItem>().HasQueryFilter(x => x.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<Income>().HasQueryFilter(x => x.UserId == CurrentBudgetOwnerUserId && !x.IsDeleted);
        modelBuilder.Entity<Loan>().HasQueryFilter(x => x.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<LoanCharge>().HasQueryFilter(x => x.Loan.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<LoanInstallment>().HasQueryFilter(x => x.Loan.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<LoanRateEntry>().HasQueryFilter(x => x.Loan.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<MonthPlan>().HasQueryFilter(x => x.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<MonthSavingsTransferItem>().HasQueryFilter(x => x.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<RegularExpenseDefinition>().HasQueryFilter(x => x.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<RegularIncomeDefinition>().HasQueryFilter(x => x.UserId == CurrentBudgetOwnerUserId);
        modelBuilder.Entity<Tag>().HasQueryFilter(x => !x.IsDeleted);
    }
}
