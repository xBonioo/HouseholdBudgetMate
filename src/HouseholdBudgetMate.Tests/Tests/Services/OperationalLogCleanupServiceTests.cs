using FluentAssertions;
using HouseholdBudgetMate.Application.Kernel.Configurations;
using HouseholdBudgetMate.Application.Kernel.Logging;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class OperationalLogCleanupServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CleanupAsync_Should_Delete_Only_Operational_Logs_Older_Than_Retention()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbContextFactory.CreateFactory(dbName);
        await SeedLogsAndAuditAsync(dbName);
        var service = CreateService(factory, new ApplicationConfiguration
        {
            LogCleanupTask = true,
            LogRetentionDays = 30
        });

        var result = await service.CleanupAsync(CancellationToken.None);

        result.IsEnabled.Should().BeTrue();
        result.DatabaseAvailable.Should().BeTrue();
        result.DeletedCount.Should().Be(1);

        await using var dbContext = TestDbContextFactory.CreateDbContext(dbName);
        var logs = await dbContext.Logs.OrderBy(x => x.Message).ToListAsync();
        logs.Should().ContainSingle();
        logs[0].Message.Should().Be("new-log");
        (await dbContext.AuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task StartAsync_Should_Propagate_When_Cleanup_Dependency_Fails()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IDbContextFactory<ApplicationDbContext>>(new ThrowingDbContextFactory())
            .AddSingleton(new ApplicationConfiguration
            {
                LogCleanupTask = true,
                LogRetentionDays = 30
            })
            .AddSingleton<IDateTimeProvider>(new StaticDateTimeProvider(NowUtc))
            .AddSingleton<ILogger<OperationalLogCleanupService>>(NullLogger<OperationalLogCleanupService>.Instance)
            .AddSingleton<OperationalLogCleanupService>()
            .BuildServiceProvider();

        var hostedService = new OperationalLogCleanupHostedService(
            provider,
            NullLogger<OperationalLogCleanupHostedService>.Instance);

        Func<Task> act = () => hostedService.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("sensitive connection failure");
    }

    [Fact]
    public async Task CleanupAsync_Should_Skip_When_Disabled()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbContextFactory.CreateFactory(dbName);
        await SeedLogsAndAuditAsync(dbName);
        var service = CreateService(factory, new ApplicationConfiguration
        {
            LogCleanupTask = false,
            LogRetentionDays = 30
        });

        var result = await service.CleanupAsync(CancellationToken.None);

        result.IsEnabled.Should().BeFalse();

        await using var dbContext = TestDbContextFactory.CreateDbContext(dbName);
        (await dbContext.Logs.CountAsync()).Should().Be(2);
        (await dbContext.AuditLogs.CountAsync()).Should().Be(1);
    }

    private static OperationalLogCleanupService CreateService(
        IDbContextFactory<ApplicationDbContext> factory,
        ApplicationConfiguration configuration)
    {
        return new OperationalLogCleanupService(
            factory,
            configuration,
            new StaticDateTimeProvider(NowUtc),
            NullLogger<OperationalLogCleanupService>.Instance);
    }

    private static async Task SeedLogsAndAuditAsync(string dbName)
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(dbName);
        dbContext.Logs.AddRange(
            new LogEntry
            {
                Message = "old-log",
                MessageTemplate = "old-log",
                Level = "Information",
                Timestamp = NowUtc.AddDays(-31)
            },
            new LogEntry
            {
                Message = "new-log",
                MessageTemplate = "new-log",
                Level = "Information",
                Timestamp = NowUtc.AddDays(-1)
            });
        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityType = nameof(Account),
            EntityId = 1,
            UserId = User.DefaultUserId,
            BudgetOwnerUserId = User.DefaultUserId,
            Operation = "Update",
            OldValuesJson = "{}",
            NewValuesJson = "{}",
            ChangedAtUtc = NowUtc.AddDays(-365)
        });
        await dbContext.SaveChangesAsync();
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
