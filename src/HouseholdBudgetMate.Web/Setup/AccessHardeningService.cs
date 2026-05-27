using HouseholdBudgetMate.Application.Security;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Web.Setup;

public interface IAccessHardeningService
{
    Task<bool> IsRequiredAsync(CancellationToken cancellationToken);
    Task<AccessHardeningResult> EstablishAdministratorAsync(
        string username,
        string pin,
        string? localAccessGrant,
        CancellationToken cancellationToken);
}

public sealed class AccessHardeningService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILocalAccessGrantService localAccessGrantService)
    : IAccessHardeningService
{
    private static readonly SemaphoreSlim EstablishmentLock = new(1, 1);

    public async Task<bool> IsRequiredAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return !await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != User.DefaultUserId
                     && x.IsAdmin
                     && x.PasswordHash.StartsWith("PBKDF2-SHA256:"),
                cancellationToken);
    }

    public async Task<AccessHardeningResult> EstablishAdministratorAsync(
        string username,
        string pin,
        string? localAccessGrant,
        CancellationToken cancellationToken)
    {
        if (!localAccessGrantService.IsValid(localAccessGrant, LocalAccessPurposes.AccessHardening))
        {
            return AccessHardeningResult.Failed("Konfiguracja dostepu jest dostepna tylko lokalnie.");
        }

        username = username.Trim();
        if (username.Length is < 3 or > 100)
        {
            return AccessHardeningResult.Failed("Nazwa użytkownika musi mieć od 3 do 100 znaków.");
        }

        string pinHash;
        try
        {
            pinHash = PinHasher.Hash(pin);
        }
        catch (ArgumentException ex)
        {
            return AccessHardeningResult.Failed(ex.Message);
        }

        await EstablishmentLock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            if (await HasSecureInteractiveAdministratorAsync(dbContext, cancellationToken))
            {
                return AccessHardeningResult.Failed("Bezpieczny administrator jest juz skonfigurowany.");
            }

            if (!localAccessGrantService.TryConsume(localAccessGrant, LocalAccessPurposes.AccessHardening))
            {
                return AccessHardeningResult.Failed("Lokalne uprawnienie konfiguracji wygaslo.");
            }

            var technicalOwner = await dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == User.DefaultUserId, cancellationToken);

            if (technicalOwner is null)
            {
                technicalOwner = new User
                {
                    Id = User.DefaultUserId,
                    Username = User.TechnicalOwnerUsername,
                    PasswordHash = string.Empty,
                    BudgetOwnerUserId = User.DefaultUserId,
                    IsAdmin = false
                };
                dbContext.Users.Add(technicalOwner);
            }
            else
            {
                technicalOwner.Username = await ResolveTechnicalOwnerUsernameAsync(dbContext, cancellationToken);
                technicalOwner.PasswordHash = string.Empty;
                technicalOwner.BudgetOwnerUserId = User.DefaultUserId;
                technicalOwner.IsAdmin = false;
            }

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
            return AccessHardeningResult.Success();
        }
        finally
        {
            EstablishmentLock.Release();
        }
    }

    private static Task<bool> HasSecureInteractiveAdministratorAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != User.DefaultUserId
                     && x.IsAdmin
                     && x.PasswordHash.StartsWith("PBKDF2-SHA256:"),
                cancellationToken);
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
}

public sealed class AccessHardeningResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    public static AccessHardeningResult Success()
    {
        return new AccessHardeningResult { IsSuccess = true };
    }

    public static AccessHardeningResult Failed(string errorMessage)
    {
        return new AccessHardeningResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
