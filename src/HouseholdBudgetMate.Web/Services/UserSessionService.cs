using HouseholdBudgetMate.Abstractions.Contracts.Users.Dto;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Domain.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;

namespace HouseholdBudgetMate.Web.Services;

public interface IUserSessionService
{
    UserDto? CurrentUser { get; }
    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken);
    Task<bool> TryRestoreFromCookieAsync(CancellationToken cancellationToken);
    Task<UserSessionSignInResult> SignInAsync(string userId, string pin, CancellationToken cancellationToken);
    Task SignOutAsync();
}

public sealed class UserSessionService(
    IUserService userService,
    CurrentUserContext currentUserContext,
    IJSRuntime jsRuntime,
    IDataProtectionProvider dataProtectionProvider) : IUserSessionService
{
    private const string CookieName = "hbm_current_user_id";
    private const int CookieDays = 30;
    private readonly IDataProtector _cookieProtector = dataProtectionProvider.CreateProtector("HouseholdBudgetMate.CurrentUserCookie");

    public UserDto? CurrentUser { get; private set; }

    public Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return userService.GetSignInUsersAsync(cancellationToken);
    }

    public async Task<bool> TryRestoreFromCookieAsync(CancellationToken cancellationToken)
    {
        var userId = await jsRuntime.InvokeAsync<string?>(
            "householdBudgetMate.cookies.get",
            cancellationToken,
            new object?[] { CookieName });

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        try
        {
            userId = _cookieProtector.Unprotect(userId);
        }
        catch
        {
            await DeleteCookieAsync();
            return false;
        }

        var users = await userService.GetUsersAsync(cancellationToken);
        var user = users.FirstOrDefault(x => string.Equals(x.Id, userId, StringComparison.Ordinal));
        if (user is null)
        {
            await DeleteCookieAsync();
            return false;
        }

        ApplyUser(user);
        return true;
    }

    public async Task<UserSessionSignInResult> SignInAsync(
        string userId,
        string pin,
        CancellationToken cancellationToken)
    {
        var users = await userService.GetUsersAsync(cancellationToken);
        var user = users.FirstOrDefault(x => string.Equals(x.Id, userId, StringComparison.Ordinal));
        if (user is null)
        {
            return UserSessionSignInResult.Failed("Nie znaleziono użytkownika.");
        }

        if (!user.IsInteractive || !user.HasPin)
        {
            return UserSessionSignInResult.Failed("Ten profil nie jest dostępny do logowania.");
        }

        var isValidPin = await userService.ValidatePinAsync(user.Id, pin, cancellationToken);
        if (!isValidPin)
        {
            return UserSessionSignInResult.Failed("Nieprawidłowy PIN.");
        }

        ApplyUser(user);
        await jsRuntime.InvokeVoidAsync(
            "householdBudgetMate.cookies.set",
            cancellationToken,
            new object?[] { CookieName, _cookieProtector.Protect(user.Id), CookieDays });

        return UserSessionSignInResult.Success(user);
    }

    public async Task SignOutAsync()
    {
        CurrentUser = null;
        currentUserContext.UserId = string.Empty;
        currentUserContext.BudgetOwnerUserId = null;
        await DeleteCookieAsync();
    }

    private void ApplyUser(UserDto user)
    {
        CurrentUser = user;
        currentUserContext.UserId = user.Id;
        currentUserContext.BudgetOwnerUserId = user.BudgetOwnerUserId;
    }

    private ValueTask DeleteCookieAsync()
    {
        return jsRuntime.InvokeVoidAsync("householdBudgetMate.cookies.delete", CookieName);
    }
}

public sealed class UserSessionSignInResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public UserDto? User { get; init; }

    public static UserSessionSignInResult Success(UserDto user)
    {
        return new UserSessionSignInResult
        {
            IsSuccess = true,
            User = user
        };
    }

    public static UserSessionSignInResult Failed(string errorMessage)
    {
        return new UserSessionSignInResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
