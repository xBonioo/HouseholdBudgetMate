using FluentAssertions;
using HouseholdBudgetMate.Application.Security;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Tests.Shared;
using HouseholdBudgetMate.Web.Setup;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class AccessHardeningServiceTests
{
    [Fact]
    public async Task IsRequiredAsync_Should_Return_True_When_Only_Technical_Owner_Exists()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "Admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId,
            IsAdmin = true
        });
        await dbContext.SaveChangesAsync();

        var service = new AccessHardeningService(factory);

        var result = await service.IsRequiredAsync(CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EstablishAdministratorAsync_Should_Promote_Profile_And_Hide_Technical_Owner()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User
            {
                Id = User.DefaultUserId,
                Username = "Admin",
                PasswordHash = string.Empty,
                BudgetOwnerUserId = User.DefaultUserId,
                IsAdmin = true
            },
            new User
            {
                Id = "member",
                Username = "Kamil",
                PasswordHash = PinHasher.Hash("1111"),
                BudgetOwnerUserId = User.DefaultUserId,
                IsAdmin = false
            });
        await dbContext.SaveChangesAsync();

        var service = new AccessHardeningService(factory);

        var result = await service.EstablishAdministratorAsync("Kamil", "5678", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var technicalOwner = await verificationContext.Users.FindAsync(User.DefaultUserId);
        technicalOwner!.Username.Should().Be(User.TechnicalOwnerUsername);
        technicalOwner.IsAdmin.Should().BeFalse();
        technicalOwner.PasswordHash.Should().BeEmpty();

        var administrator = await verificationContext.Users.FindAsync("member");
        administrator!.IsAdmin.Should().BeTrue();
        administrator.BudgetOwnerUserId.Should().Be(User.DefaultUserId);
        PinHasher.Verify("5678", administrator.PasswordHash).Should().BeTrue();
        (await service.IsRequiredAsync(CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task EstablishAdministratorAsync_Should_Create_Profile_When_Legacy_Database_Has_No_Member()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "Admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId,
            IsAdmin = true
        });
        await dbContext.SaveChangesAsync();

        var service = new AccessHardeningService(factory);

        var result = await service.EstablishAdministratorAsync("Household Admin", "9876", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await using var verificationContext = await factory.CreateDbContextAsync();
        var administrator = verificationContext.Users.Single(x => x.Id != User.DefaultUserId);
        administrator.Username.Should().Be("Household Admin");
        administrator.IsAdmin.Should().BeTrue();
        administrator.BudgetOwnerUserId.Should().Be(User.DefaultUserId);
        PinHasher.Verify("9876", administrator.PasswordHash).Should().BeTrue();
    }
}
