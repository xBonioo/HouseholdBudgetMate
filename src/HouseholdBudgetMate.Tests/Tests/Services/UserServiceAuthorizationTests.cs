using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Contracts.Users.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Security;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class UserServiceAuthorizationTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static UserService BuildService(
        IDbContextFactory<ApplicationDbContext> factory,
        string userId) =>
        new(factory, new CurrentUserContext { UserId = userId });

    // ── CreateUserAsync ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that CreateUserAsync throws ForbiddenException when the caller is not an admin.
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_Should_Require_Admin_User()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = "standard-user",
            Username = "standard",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = "standard-user",
            IsAdmin = false
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, "standard-user");

        await service.Invoking(s => s.CreateUserAsync(
                new CreateUserRequest { Username = "new-user", Pin = "1234", HouseholdMode = HouseholdMode.SeparateBudget },
                CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }

    /// <summary>
    /// Verifies that an admin can create a user with SeparateBudget mode.
    /// The new user's BudgetOwnerUserId should be set to their own generated ID.
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_Should_Create_User_With_SeparateBudget_Mode()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId,
            IsAdmin = true
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);
        var result = await service.CreateUserAsync(new CreateUserRequest
        {
            Username = "alice",
            Pin = "5678",
            HouseholdMode = HouseholdMode.SeparateBudget
        }, CancellationToken.None);

        result.Username.Should().Be("alice");
        result.HouseholdMode.Should().Be(HouseholdMode.SeparateBudget);
        result.BudgetOwnerUserId.Should().Be(result.Id);
    }

    /// <summary>
    /// Verifies that an admin can create a SharedBudget user pointing to an existing budget owner.
    /// The new user's BudgetOwnerUserId should be set to the specified owner's ID.
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_Should_Create_User_With_SharedBudget_Mode()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId,
            IsAdmin = true
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);
        var result = await service.CreateUserAsync(new CreateUserRequest
        {
            Username = "spouse",
            Pin = "9999",
            HouseholdMode = HouseholdMode.SharedBudget,
            BudgetOwnerUserId = User.DefaultUserId
        }, CancellationToken.None);

        result.HouseholdMode.Should().Be(HouseholdMode.SharedBudget);
        result.BudgetOwnerUserId.Should().Be(User.DefaultUserId);
    }

    /// <summary>
    /// Verifies that CreateUserAsync throws ConflictException when the username is already taken.
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_Should_Throw_Conflict_When_Username_Is_Duplicate()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User { Id = User.DefaultUserId, Username = "admin", PasswordHash = string.Empty, BudgetOwnerUserId = User.DefaultUserId, IsAdmin = true },
            new User { Id = "existing", Username = "alice", PasswordHash = string.Empty, BudgetOwnerUserId = "existing" });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        await service.Invoking(s => s.CreateUserAsync(
                new CreateUserRequest { Username = "alice", Pin = "1234", HouseholdMode = HouseholdMode.SeparateBudget },
                CancellationToken.None))
            .Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// Verifies that CreateUserAsync throws BadRequestException when the username is too short (under 3 characters).
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_Should_Throw_BadRequest_When_Username_Too_Short()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId,
            IsAdmin = true
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        await service.Invoking(s => s.CreateUserAsync(
                new CreateUserRequest { Username = "ab", Pin = "1234", HouseholdMode = HouseholdMode.SeparateBudget },
                CancellationToken.None))
            .Should().ThrowAsync<BadRequestException>();
    }

    /// <summary>
    /// Verifies that CreateUserAsync throws BadRequestException when the PIN contains non-digit characters.
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_Should_Throw_BadRequest_When_Pin_Is_Invalid()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId,
            IsAdmin = true
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        await service.Invoking(s => s.CreateUserAsync(
                new CreateUserRequest { Username = "alice", Pin = "abcd", HouseholdMode = HouseholdMode.SeparateBudget },
                CancellationToken.None))
            .Should().ThrowAsync<BadRequestException>();
    }

    /// <summary>
    /// Verifies that CreateUserAsync throws NotFoundException when the specified SharedBudget owner does not exist.
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_Should_Throw_NotFoundException_When_SharedBudget_Owner_Not_Found()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId,
            IsAdmin = true
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        await service.Invoking(s => s.CreateUserAsync(
                new CreateUserRequest
                {
                    Username = "spouse",
                    Pin = "1234",
                    HouseholdMode = HouseholdMode.SharedBudget,
                    BudgetOwnerUserId = "non-existent-owner"
                },
                CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ── UpdateUserAdminRoleAsync ─────────────────────────────────────────────

    /// <summary>
    /// Verifies that an admin can grant admin role to another user.
    /// </summary>
    [Fact]
    public async Task UpdateUserAdminRoleAsync_Should_Allow_Admin_To_Grant_Admin_Role()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User
            {
                Id = User.DefaultUserId,
                Username = "root",
                PasswordHash = string.Empty,
                BudgetOwnerUserId = User.DefaultUserId,
                IsAdmin = true
            },
            new User
            {
                Id = "standard-user",
                Username = "standard",
                PasswordHash = string.Empty,
                BudgetOwnerUserId = "standard-user",
                IsAdmin = false
            });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        var updated = await service.UpdateUserAdminRoleAsync(
            new UpdateUserAdminRoleRequest { UserId = "standard-user", IsAdmin = true },
            CancellationToken.None);

        updated.IsAdmin.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that UpdateUserAdminRoleAsync throws ForbiddenException when the caller is not an admin.
    /// </summary>
    [Fact]
    public async Task UpdateUserAdminRoleAsync_Should_Throw_Forbidden_When_Non_Admin()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User { Id = User.DefaultUserId, Username = "admin", PasswordHash = string.Empty, BudgetOwnerUserId = User.DefaultUserId },
            new User { Id = "regular", Username = "regular", PasswordHash = string.Empty, BudgetOwnerUserId = "regular", IsAdmin = false });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, "regular");

        await service.Invoking(s => s.UpdateUserAdminRoleAsync(
                new UpdateUserAdminRoleRequest { UserId = User.DefaultUserId, IsAdmin = false },
                CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }

    /// <summary>
    /// Verifies that UpdateUserAdminRoleAsync throws BadRequestException when attempting to revoke the default admin's role.
    /// </summary>
    [Fact]
    public async Task UpdateUserAdminRoleAsync_Should_Throw_BadRequest_When_Revoking_Default_Admin_Role()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId,
            IsAdmin = true
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        await service.Invoking(s => s.UpdateUserAdminRoleAsync(
                new UpdateUserAdminRoleRequest { UserId = User.DefaultUserId, IsAdmin = false },
                CancellationToken.None))
            .Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Default administrator*");
    }

    /// <summary>
    /// Verifies that UpdateUserAdminRoleAsync throws BadRequestException when an admin tries to revoke their own role.
    /// </summary>
    [Fact]
    public async Task UpdateUserAdminRoleAsync_Should_Throw_BadRequest_When_Admin_Revokes_Own_Role()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User { Id = User.DefaultUserId, Username = "root", PasswordHash = string.Empty, BudgetOwnerUserId = User.DefaultUserId },
            new User { Id = "admin2", Username = "admin2", PasswordHash = string.Empty, BudgetOwnerUserId = "admin2", IsAdmin = true });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, "admin2");

        await service.Invoking(s => s.UpdateUserAdminRoleAsync(
                new UpdateUserAdminRoleRequest { UserId = "admin2", IsAdmin = false },
                CancellationToken.None))
            .Should().ThrowAsync<BadRequestException>()
            .WithMessage("*own Admin*");
    }

    /// <summary>
    /// Verifies that UpdateUserAdminRoleAsync throws NotFoundException when the target user does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateUserAdminRoleAsync_Should_Throw_NotFoundException_When_User_Not_Found()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        await service.Invoking(s => s.UpdateUserAdminRoleAsync(
                new UpdateUserAdminRoleRequest { UserId = "ghost", IsAdmin = true },
                CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ── UpdateUserBudgetModeAsync ────────────────────────────────────────────

    /// <summary>
    /// Verifies that UpdateUserBudgetModeAsync throws ForbiddenException when the caller is not an admin.
    /// </summary>
    [Fact]
    public async Task UpdateUserBudgetModeAsync_Should_Require_Admin()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User { Id = User.DefaultUserId, Username = "admin", PasswordHash = string.Empty, BudgetOwnerUserId = User.DefaultUserId },
            new User { Id = "regular", Username = "regular", PasswordHash = string.Empty, BudgetOwnerUserId = "regular", IsAdmin = false });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, "regular");

        await service.Invoking(s => s.UpdateUserBudgetModeAsync(
                new UpdateUserBudgetModeRequest { UserId = "regular", HouseholdMode = HouseholdMode.SeparateBudget },
                CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }

    /// <summary>
    /// Verifies that an admin can switch a user to SeparateBudget mode,
    /// and the user's BudgetOwnerUserId is set to their own ID.
    /// </summary>
    [Fact]
    public async Task UpdateUserBudgetModeAsync_Should_Update_To_SeparateBudget()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User { Id = User.DefaultUserId, Username = "admin", PasswordHash = string.Empty, BudgetOwnerUserId = User.DefaultUserId },
            new User { Id = "user1", Username = "user1", PasswordHash = string.Empty, BudgetOwnerUserId = User.DefaultUserId, HouseholdMode = (int)HouseholdMode.SharedBudget });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);
        var result = await service.UpdateUserBudgetModeAsync(new UpdateUserBudgetModeRequest
        {
            UserId = "user1",
            HouseholdMode = HouseholdMode.SeparateBudget
        }, CancellationToken.None);

        result.HouseholdMode.Should().Be(HouseholdMode.SeparateBudget);
        result.BudgetOwnerUserId.Should().Be("user1");
    }

    /// <summary>
    /// Verifies that UpdateUserBudgetModeAsync throws NotFoundException when the target user does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateUserBudgetModeAsync_Should_Throw_NotFoundException_When_User_Not_Found()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        await service.Invoking(s => s.UpdateUserBudgetModeAsync(
                new UpdateUserBudgetModeRequest { UserId = "ghost", HouseholdMode = HouseholdMode.SeparateBudget },
                CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ── UpdateUserPinAsync ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies that UpdateUserPinAsync throws ForbiddenException when the caller is not an admin.
    /// </summary>
    [Fact]
    public async Task UpdateUserPinAsync_Should_Require_Admin()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User { Id = User.DefaultUserId, Username = "admin", PasswordHash = string.Empty, BudgetOwnerUserId = User.DefaultUserId },
            new User { Id = "regular", Username = "regular", PasswordHash = string.Empty, BudgetOwnerUserId = "regular", IsAdmin = false });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, "regular");

        await service.Invoking(s => s.UpdateUserPinAsync(
                new UpdateUserPinRequest { UserId = "regular", Pin = "1234" },
                CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }

    /// <summary>
    /// Verifies that UpdateUserPinAsync throws BadRequestException when called for the default admin user.
    /// </summary>
    [Fact]
    public async Task UpdateUserPinAsync_Should_Throw_BadRequest_For_Default_Admin()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        await service.Invoking(s => s.UpdateUserPinAsync(
                new UpdateUserPinRequest { UserId = User.DefaultUserId, Pin = "1234" },
                CancellationToken.None))
            .Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Default administrator*");
    }

    /// <summary>
    /// Verifies that an admin can update a user's PIN and the new PIN is validated successfully afterwards.
    /// </summary>
    [Fact]
    public async Task UpdateUserPinAsync_Should_Update_Pin_Hash()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User { Id = User.DefaultUserId, Username = "admin", PasswordHash = string.Empty, BudgetOwnerUserId = User.DefaultUserId },
            new User { Id = "user1", Username = "user1", PasswordHash = string.Empty, BudgetOwnerUserId = "user1" });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);
        await service.UpdateUserPinAsync(new UpdateUserPinRequest { UserId = "user1", Pin = "5678" }, CancellationToken.None);

        var valid = await service.ValidatePinAsync("user1", "5678", CancellationToken.None);
        valid.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that UpdateUserPinAsync throws NotFoundException when the user does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateUserPinAsync_Should_Throw_NotFoundException_When_User_Not_Found()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        await service.Invoking(s => s.UpdateUserPinAsync(
                new UpdateUserPinRequest { UserId = "ghost", Pin = "1234" },
                CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ── ValidatePinAsync ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that ValidatePinAsync returns true when the correct PIN is provided.
    /// </summary>
    [Fact]
    public async Task ValidatePinAsync_Should_Return_True_For_Correct_Pin()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User { Id = User.DefaultUserId, Username = "admin", PasswordHash = string.Empty, BudgetOwnerUserId = User.DefaultUserId },
            new User { Id = "user1", Username = "user1", PasswordHash = string.Empty, BudgetOwnerUserId = "user1" });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);
        await service.UpdateUserPinAsync(new UpdateUserPinRequest { UserId = "user1", Pin = "4321" }, CancellationToken.None);

        var result = await service.ValidatePinAsync("user1", "4321", CancellationToken.None);
        result.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that ValidatePinAsync returns false when the wrong PIN is provided.
    /// </summary>
    [Fact]
    public async Task ValidatePinAsync_Should_Return_False_For_Wrong_Pin()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User { Id = User.DefaultUserId, Username = "admin", PasswordHash = string.Empty, BudgetOwnerUserId = User.DefaultUserId },
            new User { Id = "user1", Username = "user1", PasswordHash = string.Empty, BudgetOwnerUserId = "user1" });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);
        await service.UpdateUserPinAsync(new UpdateUserPinRequest { UserId = "user1", Pin = "4321" }, CancellationToken.None);

        var result = await service.ValidatePinAsync("user1", "9999", CancellationToken.None);
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that the technical budget owner cannot be used as a PIN-less sign-in profile.
    /// </summary>
    [Fact]
    public async Task ValidatePinAsync_Should_Return_False_For_Technical_Owner_With_Empty_Pin()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        var result = await service.ValidatePinAsync(User.DefaultUserId, string.Empty, CancellationToken.None);
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that ValidatePinAsync returns false for an unknown user ID.
    /// </summary>
    [Fact]
    public async Task ValidatePinAsync_Should_Return_False_For_Unknown_User()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        var result = await service.ValidatePinAsync("ghost-user", "1234", CancellationToken.None);
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that ValidatePinAsync returns false when an empty user ID is provided.
    /// </summary>
    [Fact]
    public async Task ValidatePinAsync_Should_Return_False_For_Empty_UserId()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "admin",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, User.DefaultUserId);

        var result = await service.ValidatePinAsync(string.Empty, "1234", CancellationToken.None);
        result.Should().BeFalse();
    }

    // ── GetUsersAsync ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that GetUsersAsync returns all users ordered by username.
    /// </summary>
    [Fact]
    public async Task GetUsersAsync_Should_Return_All_Users_Ordered_By_Username()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User { Id = "u3", Username = "zebra", PasswordHash = string.Empty, BudgetOwnerUserId = "u3" },
            new User { Id = "u1", Username = "apple", PasswordHash = string.Empty, BudgetOwnerUserId = "u1" },
            new User { Id = "u2", Username = "mango", PasswordHash = string.Empty, BudgetOwnerUserId = "u2" });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, "u1");
        var users = await service.GetUsersAsync(CancellationToken.None);

        users.Should().HaveCount(3);
        users.Select(u => u.Username).Should().Equal("apple", "mango", "zebra");
    }

    /// <summary>
    /// Verifies that only visible profiles with configured PINs are offered for sign-in.
    /// </summary>
    [Fact]
    public async Task GetSignInUsersAsync_Should_Exclude_Technical_Owner_And_Pinless_Profile()
    {
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User
            {
                Id = User.DefaultUserId,
                Username = User.TechnicalOwnerUsername,
                PasswordHash = string.Empty,
                BudgetOwnerUserId = User.DefaultUserId,
                IsAdmin = true
            },
            new User { Id = "pinless", Username = "pinless", PasswordHash = string.Empty, BudgetOwnerUserId = "pinless" },
            new User { Id = "secured", Username = "secured", PasswordHash = PinHasher.Hash("1234"), BudgetOwnerUserId = "secured" });
        await dbContext.SaveChangesAsync();

        var service = BuildService(factory, "secured");

        var users = await service.GetSignInUsersAsync(CancellationToken.None);

        users.Should().ContainSingle();
        users.Single().Id.Should().Be("secured");
        users.Single().IsInteractive.Should().BeTrue();
    }
}
