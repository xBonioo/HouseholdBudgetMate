using FluentAssertions;
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
