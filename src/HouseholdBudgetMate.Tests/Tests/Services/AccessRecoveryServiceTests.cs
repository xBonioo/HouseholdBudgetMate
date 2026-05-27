using System.Text.Json;
using FluentAssertions;
using HouseholdBudgetMate.Application.Security;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Tests.Shared;
using HouseholdBudgetMate.Web.Setup;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class AccessRecoveryServiceTests
{
    [Fact]
    public async Task RecoverAdministratorAsync_Should_Reject_Reset_When_Local_Mode_Is_Disabled()
    {
        var runtimeState = CreateRuntimeState(localAccessRecoveryEnabled: false);
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        var service = new AccessRecoveryService(runtimeState, factory);

        var result = await service.RecoverAdministratorAsync("Admin", "5678", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("nie jest włączony");
    }

    [Fact]
    public async Task RecoverAdministratorAsync_Should_Reset_Visible_Admin_And_Disable_Recovery_Mode()
    {
        var runtimeState = CreateRuntimeState(localAccessRecoveryEnabled: true);
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.AddRange(
            new User
            {
                Id = User.DefaultUserId,
                Username = User.TechnicalOwnerUsername,
                PasswordHash = string.Empty,
                BudgetOwnerUserId = User.DefaultUserId,
                IsAdmin = false
            },
            new User
            {
                Id = "visible-admin",
                Username = "Admin",
                PasswordHash = PinHasher.Hash("1234"),
                BudgetOwnerUserId = User.DefaultUserId,
                IsAdmin = true
            });
        await dbContext.SaveChangesAsync();
        var service = new AccessRecoveryService(runtimeState, factory);

        var result = await service.RecoverAdministratorAsync("Admin", "5678", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        runtimeState.IsLocalAccessRecoveryEnabled.Should().BeFalse();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var administrator = await verificationContext.Users.FindAsync("visible-admin");
        administrator!.IsAdmin.Should().BeTrue();
        administrator.BudgetOwnerUserId.Should().Be(User.DefaultUserId);
        PinHasher.Verify("5678", administrator.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task RecoverAdministratorAsync_Should_Establish_New_Visible_Admin_Without_Interactive_Technical_Owner()
    {
        var runtimeState = CreateRuntimeState(localAccessRecoveryEnabled: true);
        await using var dbContext = TestDbContextFactory.CreateDbContext(out var factory);
        dbContext.Users.Add(new User
        {
            Id = User.DefaultUserId,
            Username = "Old owner",
            PasswordHash = string.Empty,
            BudgetOwnerUserId = User.DefaultUserId,
            IsAdmin = true
        });
        await dbContext.SaveChangesAsync();
        var service = new AccessRecoveryService(runtimeState, factory);

        var result = await service.RecoverAdministratorAsync("Replacement Admin", "9876", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var technicalOwner = await verificationContext.Users.FindAsync(User.DefaultUserId);
        technicalOwner!.IsAdmin.Should().BeFalse();
        technicalOwner.PasswordHash.Should().BeEmpty();

        var administrator = verificationContext.Users.Single(x => x.Id != User.DefaultUserId);
        administrator.IsAdmin.Should().BeTrue();
        PinHasher.Verify("9876", administrator.PasswordHash).Should().BeTrue();
    }

    private static RuntimeConfigurationState CreateRuntimeState(bool localAccessRecoveryEnabled)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hbm-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var state = new RuntimeConfigurationState(directory);
        var config = new RuntimeConfigurationState.RuntimeAppConfiguration
        {
            Database = new RuntimeDatabaseConfiguration
            {
                Host = "localhost",
                Port = 5432,
                Username = "test",
                Password = "test",
                Database = "hbm"
            },
            LocalAccessRecoveryEnabled = localAccessRecoveryEnabled
        };
        File.WriteAllText(
            state.ConfigFilePath,
            JsonSerializer.Serialize(config, RuntimeConfigurationState.JsonOptions));
        state.ReloadFromDisk();
        return state;
    }
}
