using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Shared;

public static class TestDbContextFactory
{
    public static ApplicationDbContext CreateDbContext(string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString();
        var options = BuildOptions(name);
        return new ApplicationDbContext(options, CreateCurrentUserContext());
    }

    public static ApplicationDbContext CreateDbContext(out IDbContextFactory<ApplicationDbContext> factory, string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString();
        var options = BuildOptions(name);
        factory = new InMemoryDbContextFactory(options);
        return new ApplicationDbContext(options, CreateCurrentUserContext());
    }

    public static IDbContextFactory<ApplicationDbContext> CreateFactory(string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString();
        var options = BuildOptions(name);
        return new InMemoryDbContextFactory(options);
    }

    public static IDbContextFactory<ApplicationDbContext> CreateThrowingFactory(string dbName, bool throwOnSave)
    {
        var options = BuildOptions(dbName);
        return new ThrowingDbContextFactory(options, throwOnSave);
    }

    private static DbContextOptions<ApplicationDbContext> BuildOptions(string dbName)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
    }

    private static CurrentUserContext CreateCurrentUserContext()
    {
        return new CurrentUserContext
        {
            UserId = User.DefaultUserId,
            BudgetOwnerUserId = User.DefaultUserId
        };
    }

    private sealed class InMemoryDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options, CreateCurrentUserContext());

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class ThrowingDbContextFactory(DbContextOptions<ApplicationDbContext> options, bool throwOnSave)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new ThrowingApplicationDbContext(options, throwOnSave);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
