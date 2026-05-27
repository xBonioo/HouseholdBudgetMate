using System.Text.Json;
using HouseholdBudgetMate.Application.Security;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Web.Setup;

public interface IAccessRecoveryService
{
    bool IsRecoveryRequired { get; }
    Task<AccessRecoveryResult> RecoverAdministratorAsync(
        string username,
        string pin,
        string? localAccessGrant,
        CancellationToken cancellationToken);
}

public sealed class AccessRecoveryService(
    RuntimeConfigurationState runtimeConfigurationState,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILocalAccessGrantService localAccessGrantService) : IAccessRecoveryService
{
    private static readonly SemaphoreSlim RecoveryLock = new(1, 1);

    public bool IsRecoveryRequired => runtimeConfigurationState.IsLocalAccessRecoveryEnabled;

    public async Task<AccessRecoveryResult> RecoverAdministratorAsync(
        string username,
        string pin,
        string? localAccessGrant,
        CancellationToken cancellationToken)
    {
        if (!localAccessGrantService.IsValid(localAccessGrant, LocalAccessPurposes.AccessRecovery))
        {
            return AccessRecoveryResult.Failed("Odzyskiwanie dostepu jest dostepne tylko lokalnie.");
        }

        if (!IsRecoveryRequired)
        {
            return AccessRecoveryResult.Failed("Lokalny tryb odzyskiwania nie jest włączony.");
        }

        username = username.Trim();
        if (username.Length is < 3 or > 100)
        {
            return AccessRecoveryResult.Failed("Nazwa administratora musi mieć od 3 do 100 znaków.");
        }

        string pinHash;
        try
        {
            pinHash = PinHasher.Hash(pin);
        }
        catch (ArgumentException ex)
        {
            return AccessRecoveryResult.Failed(ex.Message);
        }

        await RecoveryLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsRecoveryRequired)
            {
                return AccessRecoveryResult.Failed("Lokalny tryb odzyskiwania nie jest wlaczony.");
            }

            if (!localAccessGrantService.TryConsume(localAccessGrant, LocalAccessPurposes.AccessRecovery))
            {
                return AccessRecoveryResult.Failed("Lokalne uprawnienie odzyskiwania wygaslo.");
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var technicalOwner = await dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == User.DefaultUserId, cancellationToken);

            if (technicalOwner is null)
            {
                technicalOwner = new User
                {
                    Id = User.DefaultUserId,
                    Username = User.TechnicalOwnerUsername,
                    BudgetOwnerUserId = User.DefaultUserId
                };
                dbContext.Users.Add(technicalOwner);
            }

            technicalOwner.Username = await ResolveTechnicalOwnerUsernameAsync(dbContext, cancellationToken);
            technicalOwner.PasswordHash = string.Empty;
            technicalOwner.IsAdmin = false;
            technicalOwner.BudgetOwnerUserId = User.DefaultUserId;

            var administrator = await dbContext.Users
                .FirstOrDefaultAsync(
                    x => x.Id != User.DefaultUserId && x.Username.ToUpper() == username.ToUpper(),
                    cancellationToken);

            if (administrator is null)
            {
                administrator = new User
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Username = username,
                    HouseholdMode = 1,
                    BudgetOwnerUserId = User.DefaultUserId
                };
                dbContext.Users.Add(administrator);
            }

            administrator.PasswordHash = pinHash;
            administrator.IsAdmin = true;
            administrator.BudgetOwnerUserId = User.DefaultUserId;

            await dbContext.SaveChangesAsync(cancellationToken);
            var disableResult = await DisableRecoveryModeAsync(cancellationToken);
            return disableResult.IsSuccess ? AccessRecoveryResult.Success() : disableResult;
        }
        finally
        {
            RecoveryLock.Release();
        }
    }

    private static async Task<string> ResolveTechnicalOwnerUsernameAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var username = User.TechnicalOwnerUsername;
        var suffix = 0;

        while (await dbContext.Users.AnyAsync(
                   x => x.Id != User.DefaultUserId && x.Username == username,
                   cancellationToken))
        {
            suffix++;
            username = $"{User.TechnicalOwnerUsername}-{suffix}";
        }

        return username;
    }

    private async Task<AccessRecoveryResult> DisableRecoveryModeAsync(CancellationToken cancellationToken)
    {
        var database = runtimeConfigurationState.GetDatabaseConfiguration();
        if (database is null)
        {
            return AccessRecoveryResult.Failed("Brak skonfigurowanego połączenia z bazą danych.");
        }

        var configuration = new RuntimeConfigurationState.RuntimeAppConfiguration
        {
            Database = database,
            HouseholdMode = runtimeConfigurationState.GetHouseholdMode(),
            SharedWithUserIds = runtimeConfigurationState.GetSharedWithUserIds(),
            LocalAccessRecoveryEnabled = false
        };

        try
        {
            var directory = Path.GetDirectoryName(runtimeConfigurationState.ConfigFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(configuration, RuntimeConfigurationState.JsonOptions);
            await File.WriteAllTextAsync(runtimeConfigurationState.ConfigFilePath, json, cancellationToken);
            runtimeConfigurationState.ReloadFromDisk();
            return AccessRecoveryResult.Success();
        }
        catch (Exception ex)
        {
            return AccessRecoveryResult.Failed($"Nie można wyłączyć trybu odzyskiwania: {ex.Message}");
        }
    }
}

public sealed class AccessRecoveryResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    public static AccessRecoveryResult Success()
    {
        return new AccessRecoveryResult { IsSuccess = true };
    }

    public static AccessRecoveryResult Failed(string errorMessage)
    {
        return new AccessRecoveryResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
