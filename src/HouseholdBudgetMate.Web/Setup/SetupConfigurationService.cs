using System.Text.Json;
using HouseholdBudgetMate.Application.Security;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Web.Setup;

public interface ISetupConfigurationService
{
    Task<SetupResult> SaveConfigurationAsync(SetupInputModel inputModel, CancellationToken cancellationToken);
}

public sealed class SetupConfigurationService(
    RuntimeConfigurationState runtimeConfigurationState,
    IDatabaseMigrationOrchestrator databaseMigrationOrchestrator) : ISetupConfigurationService
{
    public async Task<SetupResult> SaveConfigurationAsync(SetupInputModel inputModel, CancellationToken cancellationToken)
    {
        var runtimeDatabaseConfiguration = new RuntimeDatabaseConfiguration
        {
            Host = inputModel.Host.Trim(),
            Port = inputModel.Port,
            Username = inputModel.Username.Trim(),
            Password = inputModel.Password,
            Database = inputModel.Database.Trim()
        };

        try
        {
            await databaseMigrationOrchestrator.ValidateConnectionAndMigrateAsync(runtimeDatabaseConfiguration, cancellationToken);
            await EnsureInitialUserAsync(runtimeDatabaseConfiguration, inputModel, cancellationToken);
        }
        catch (Exception ex)
        {
            return SetupResult.Failed($"Nie można połączyć się z bazą lub wykonać migracji: {ex.Message}");
        }

        var runtimeAppConfiguration = new RuntimeConfigurationState.RuntimeAppConfiguration
        {
            Database = runtimeDatabaseConfiguration,
            HouseholdMode = inputModel.HouseholdMode,
            SharedWithUserIds = ParseSharedWithUserIds(inputModel.SharedWithUserIds)
        };

        var json = JsonSerializer.Serialize(runtimeAppConfiguration, RuntimeConfigurationState.JsonOptions);

        try
        {
            var directory = Path.GetDirectoryName(runtimeConfigurationState.ConfigFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(runtimeConfigurationState.ConfigFilePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            return SetupResult.Failed($"Nie można zapisać pliku config.json: {ex.Message}");
        }

        runtimeConfigurationState.ReloadFromDisk();
        runtimeConfigurationState.SetHouseholdMode(inputModel.HouseholdMode);
        runtimeConfigurationState.SetSharedWithUserIds(runtimeAppConfiguration.SharedWithUserIds);
        return SetupResult.Success();
    }

    private static IReadOnlyList<string> ParseSharedWithUserIds(string input)
    {
        return RuntimeConfigurationState.NormalizeUserIds(
            input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static async Task EnsureInitialUserAsync(
        RuntimeDatabaseConfiguration runtimeDatabaseConfiguration,
        SetupInputModel inputModel,
        CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                runtimeDatabaseConfiguration.ToConnectionString(),
                npgsqlOptions => npgsqlOptions.MigrationsAssembly("HouseholdBudgetMate.Migrations"))
            .Options;

        await using var dbContext = new ApplicationDbContext(
            options,
            CurrentUserContext.ForTechnicalOwner());

        var defaultAdmin = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == User.DefaultUserId, cancellationToken);
        if (defaultAdmin is null)
        {
            dbContext.Users.Add(new User
            {
                Id = User.DefaultUserId,
                Username = User.TechnicalOwnerUsername,
                PasswordHash = string.Empty,
                HouseholdMode = (int)inputModel.HouseholdMode,
                BudgetOwnerUserId = User.DefaultUserId,
                IsAdmin = false
            });
        }
        else
        {
            defaultAdmin.Username = User.TechnicalOwnerUsername;
            defaultAdmin.PasswordHash = string.Empty;
            defaultAdmin.BudgetOwnerUserId = User.DefaultUserId;
            defaultAdmin.IsAdmin = false;
        }

        var username = inputModel.AppUsername.Trim();
        var existingAppUser = await dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Id != User.DefaultUserId && x.Username.ToUpper() == username.ToUpper(),
                cancellationToken);
        var appUserBudgetOwnerId = inputModel.HouseholdMode == HouseholdBudgetMate.Abstractions.Enums.HouseholdMode.SharedBudget
            ? User.DefaultUserId
            : existingAppUser?.Id ?? Guid.NewGuid().ToString("N");
        if (existingAppUser is null)
        {
            dbContext.Users.Add(new User
            {
                Id = appUserBudgetOwnerId == User.DefaultUserId
                    ? Guid.NewGuid().ToString("N")
                    : appUserBudgetOwnerId,
                Username = username,
                PasswordHash = PinHasher.Hash(inputModel.AppPin),
                HouseholdMode = (int)inputModel.HouseholdMode,
                BudgetOwnerUserId = appUserBudgetOwnerId,
                IsAdmin = true
            });
        }
        else
        {
            existingAppUser.PasswordHash = PinHasher.Hash(inputModel.AppPin);
            existingAppUser.HouseholdMode = (int)inputModel.HouseholdMode;
            existingAppUser.BudgetOwnerUserId = appUserBudgetOwnerId;
            existingAppUser.IsAdmin = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SetupResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    public static SetupResult Success()
    {
        return new SetupResult { IsSuccess = true };
    }

    public static SetupResult Failed(string errorMessage)
    {
        return new SetupResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
