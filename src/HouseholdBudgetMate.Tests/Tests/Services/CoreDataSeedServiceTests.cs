using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Helpers;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class CoreDataSeedServiceTests
{
    [Fact]
    public async Task EnsureCurrentMonthPlanAsync_Should_Use_Explicit_Technical_Owner_Scope_And_Restore_Context()
    {
        var currentUserContext = new CurrentUserContext();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var timeProvider = new Mock<IDateTimeProvider>();
        timeProvider.Setup(x => x.GetLocalDateTime()).Returns(new DateTime(2026, 5, 27));
        var service = new CoreDataSeedService(
            new ContextFactory(options, currentUserContext),
            timeProvider.Object,
            NullLogger<CoreDataSeedService>.Instance,
            currentUserContext);

        await service.EnsureCurrentMonthPlanAsync(CancellationToken.None);

        currentUserContext.UserId.Should().BeEmpty();
        currentUserContext.BudgetOwnerUserId.Should().BeNull();
        currentUserContext.IsSystemOperation.Should().BeFalse();

        await using var verifyContext = new ApplicationDbContext(options, CurrentUserContext.ForTechnicalOwner());
        var technicalOwner = await verifyContext.Users.SingleAsync(x => x.Id == User.DefaultUserId);
        technicalOwner.Username.Should().Be(User.TechnicalOwnerUsername);
        technicalOwner.IsAdmin.Should().BeFalse();

        var plan = await verifyContext.MonthPlans.SingleAsync();
        plan.UserId.Should().Be(User.DefaultUserId);
        plan.Year.Should().Be(2026);
        plan.Month.Should().Be(5);
    }

    [Fact]
    public async Task ClearBudgetDataAsync_Should_Delete_Budget_Data_And_Keep_Users()
    {
        var currentUserContext = new CurrentUserContext();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var timeProvider = new Mock<IDateTimeProvider>();
        timeProvider.Setup(x => x.GetLocalDateTime()).Returns(new DateTime(2026, 5, 27));

        await using (var setupContext = new ApplicationDbContext(options, CurrentUserContext.ForTechnicalOwner()))
        {
            setupContext.Users.AddRange(
                new User
                {
                    Id = User.DefaultUserId,
                    Username = User.TechnicalOwnerUsername,
                    PasswordHash = string.Empty,
                    BudgetOwnerUserId = User.DefaultUserId
                },
                new User
                {
                    Id = "admin-user",
                    Username = "admin",
                    PasswordHash = "PBKDF2-SHA256:test",
                    BudgetOwnerUserId = User.DefaultUserId,
                    IsAdmin = true
                });

            var category = new Category { Name = "Food", Color = "#123456" };
            setupContext.Categories.Add(category);
            setupContext.Tags.Add(new Tag { Name = "Groceries", Category = category });
            setupContext.Accounts.Add(new Account { Name = "Bank", Type = (int)AccountType.Bank, Order = 1 });
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 5 });
            setupContext.Logs.Add(new LogEntry
            {
                Message = "log",
                MessageTemplate = "log",
                Level = "Information",
                Timestamp = DateTime.UtcNow
            });
            setupContext.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Account),
                EntityId = 1,
                UserId = User.DefaultUserId,
                BudgetOwnerUserId = User.DefaultUserId,
                Operation = "Create",
                OldValuesJson = "{}",
                NewValuesJson = "{}",
                ChangedAtUtc = DateTime.UtcNow
            });

            await setupContext.SaveChangesAsync();
        }

        var service = new CoreDataSeedService(
            new ContextFactory(options, currentUserContext),
            timeProvider.Object,
            NullLogger<CoreDataSeedService>.Instance,
            currentUserContext);

        await service.ClearBudgetDataAsync(CancellationToken.None);

        await using var verifyContext = new ApplicationDbContext(options, CurrentUserContext.ForTechnicalOwner());
        (await verifyContext.Users.CountAsync()).Should().Be(2);
        (await verifyContext.Categories.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await verifyContext.Tags.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await verifyContext.Accounts.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await verifyContext.MonthPlans.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await verifyContext.Logs.CountAsync()).Should().Be(0);
        (await verifyContext.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SeedDefaultsForCurrentBudgetAsync_Should_Seed_Visible_Data_For_Current_Budget()
    {
        var currentUserContext = new CurrentUserContext();
        currentUserContext.SetInteractiveUser("user-a", "user-a");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var timeProvider = new Mock<IDateTimeProvider>();
        timeProvider.Setup(x => x.GetLocalDateTime()).Returns(new DateTime(2026, 5, 27));

        await using (var setupContext = new ApplicationDbContext(options, CurrentUserContext.ForTechnicalOwner()))
        {
            setupContext.Users.AddRange(
                new User
                {
                    Id = User.DefaultUserId,
                    Username = User.TechnicalOwnerUsername,
                    PasswordHash = string.Empty,
                    BudgetOwnerUserId = User.DefaultUserId
                },
                new User
                {
                    Id = "user-a",
                    Username = "user-a",
                    PasswordHash = "PBKDF2-SHA256:test",
                    BudgetOwnerUserId = "user-a"
                });

            await setupContext.SaveChangesAsync();
        }

        var service = new CoreDataSeedService(
            new ContextFactory(options, currentUserContext),
            timeProvider.Object,
            NullLogger<CoreDataSeedService>.Instance,
            currentUserContext);

        await service.SeedDefaultsForCurrentBudgetAsync(CancellationToken.None);
        await service.EnsureCurrentMonthPlanForCurrentBudgetAsync(CancellationToken.None);

        await using var verifyContext = new ApplicationDbContext(options, new CurrentUserContext
        {
            UserId = "user-a",
            BudgetOwnerUserId = "user-a"
        });
        var accounts = await verifyContext.Accounts.ToListAsync();
        accounts.Should().NotBeEmpty();
        accounts.Should().OnlyContain(x => x.UserId == "user-a");

        var plan = await verifyContext.MonthPlans.SingleAsync();
        plan.UserId.Should().Be("user-a");
        plan.Year.Should().Be(2026);
        plan.Month.Should().Be(5);
    }

    private sealed class ContextFactory(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUserContext) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            return new ApplicationDbContext(options, currentUserContext);
        }

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
