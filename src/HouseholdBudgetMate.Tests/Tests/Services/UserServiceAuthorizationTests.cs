using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Contracts.Users.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Tests.Shared;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class UserServiceAuthorizationTests
{
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

        var service = new UserService(
            factory,
            new CurrentUserContext { UserId = "standard-user" });

        var act = () => service.CreateUserAsync(
            new CreateUserRequest
            {
                Username = "new-user",
                Pin = "1234",
                HouseholdMode = HouseholdMode.SeparateBudget
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

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

        var service = new UserService(
            factory,
            new CurrentUserContext { UserId = User.DefaultUserId });

        var updated = await service.UpdateUserAdminRoleAsync(
            new UpdateUserAdminRoleRequest
            {
                UserId = "standard-user",
                IsAdmin = true
            },
            CancellationToken.None);

        updated.IsAdmin.Should().BeTrue();
    }
}
