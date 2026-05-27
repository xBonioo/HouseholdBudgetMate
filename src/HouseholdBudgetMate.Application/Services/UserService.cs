using HouseholdBudgetMate.Abstractions.Contracts.Users.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Users.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Security;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class UserService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    CurrentUserContext currentUserContext) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var users = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.BudgetOwnerUser)
            .OrderBy(x => x.Username)
            .ToListAsync(cancellationToken);

        return users.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<UserDto>> GetSignInUsersAsync(CancellationToken cancellationToken)
    {
        var users = await GetUsersAsync(cancellationToken);
        return users
            .Where(x => x.IsInteractive && x.HasPin)
            .ToList();
    }

    public async Task<bool> HasSecureInteractiveAdministratorAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id != User.DefaultUserId
                     && x.IsAdmin
                     && x.PasswordHash.StartsWith("PBKDF2-SHA256:"),
                cancellationToken);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var username = NormalizeUsername(request.Username);
        var pinHash = HashPinOrThrowBadRequest(request.Pin);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);
        await EnsureUsernameUniqueAsync(dbContext, username, null, cancellationToken);

        var userId = Guid.NewGuid().ToString("N");
        var budgetOwnerUserId = await ResolveBudgetOwnerUserIdAsync(
            dbContext,
            userId,
            request.HouseholdMode,
            request.BudgetOwnerUserId,
            cancellationToken);

        var user = new User
        {
            Id = userId,
            Username = username,
            PasswordHash = pinHash,
            HouseholdMode = (int)request.HouseholdMode,
            BudgetOwnerUserId = budgetOwnerUserId,
            IsAdmin = false
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildUserDtoAsync(dbContext, user.Id, cancellationToken);
    }

    public async Task<UserDto> UpdateUserBudgetModeAsync(
        UpdateUserBudgetModeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new BadRequestException("User ID is required.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);
        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        user.HouseholdMode = (int)request.HouseholdMode;
        user.BudgetOwnerUserId = await ResolveBudgetOwnerUserIdAsync(
            dbContext,
            user.Id,
            request.HouseholdMode,
            request.BudgetOwnerUserId,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildUserDtoAsync(dbContext, user.Id, cancellationToken);
    }

    public async Task<UserDto> UpdateUserAdminRoleAsync(
        UpdateUserAdminRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new BadRequestException("User ID is required.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);

        if (request.UserId == User.DefaultUserId && !request.IsAdmin)
        {
            throw new BadRequestException("Default administrator must keep Admin permissions.");
        }

        if (request.UserId == currentUserContext.UserId && !request.IsAdmin)
        {
            throw new BadRequestException("You cannot revoke your own Admin permissions.");
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        user.IsAdmin = request.UserId == User.DefaultUserId || request.IsAdmin;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildUserDtoAsync(dbContext, user.Id, cancellationToken);
    }

    public async Task UpdateUserPinAsync(UpdateUserPinRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new BadRequestException("User ID is required.");
        }

        if (request.UserId == User.DefaultUserId)
        {
            throw new BadRequestException("Default administrator does not use a PIN.");
        }

        var pinHash = HashPinOrThrowBadRequest(request.Pin);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);
        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        user.PasswordHash = pinHash;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ValidatePinAsync(string userId, string pin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return false;
        }

        if (user.Id == User.DefaultUserId)
        {
            return false;
        }

        try
        {
            return PinHasher.Verify(pin, user.PasswordHash);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeUsername(string username)
    {
        username = username.Trim();
        if (username.Length is < 3 or > 100)
        {
            throw new BadRequestException("Username must contain 3 to 100 characters.");
        }

        return username;
    }

    private static string HashPinOrThrowBadRequest(string pin)
    {
        try
        {
            return PinHasher.Hash(pin);
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message);
        }
    }

    private static async Task EnsureUsernameUniqueAsync(
        ApplicationDbContext dbContext,
        string username,
        string? excludeUserId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Users
            .AnyAsync(x => x.Username.ToUpper() == username.ToUpper()
                           && (excludeUserId == null || x.Id != excludeUserId),
                cancellationToken);
        if (exists)
        {
            throw new ConflictException("Username must be unique.");
        }
    }

    private static async Task<string> ResolveBudgetOwnerUserIdAsync(
        ApplicationDbContext dbContext,
        string userId,
        HouseholdMode householdMode,
        string? requestedBudgetOwnerUserId,
        CancellationToken cancellationToken)
    {
        if (householdMode == HouseholdMode.SeparateBudget)
        {
            return userId;
        }

        var budgetOwnerUserId = string.IsNullOrWhiteSpace(requestedBudgetOwnerUserId)
            ? User.DefaultUserId
            : requestedBudgetOwnerUserId.Trim();

        var ownerExists = budgetOwnerUserId == userId
                          || await dbContext.Users.AnyAsync(x => x.Id == budgetOwnerUserId, cancellationToken);
        if (!ownerExists)
        {
            throw new NotFoundException("Shared budget owner user not found.");
        }

        return budgetOwnerUserId;
    }

    private static async Task<UserDto> BuildUserDtoAsync(
        ApplicationDbContext dbContext,
        string userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.BudgetOwnerUser)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        return MapToDto(user);
    }

    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            HouseholdMode = (HouseholdMode)user.HouseholdMode,
            BudgetOwnerUserId = user.BudgetOwnerUserId,
            BudgetOwnerUsername = user.BudgetOwnerUser?.Username,
            HasPin = HasConfiguredPin(user),
            IsInteractive = user.Id != User.DefaultUserId,
            IsDefaultAdmin = user.Id == User.DefaultUserId,
            IsAdmin = user.Id == User.DefaultUserId || user.IsAdmin
        };
    }

    private async Task EnsureCurrentUserIsAdminAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserContext.UserId))
        {
            throw new ForbiddenException("Admin permissions are required.");
        }

        var isAdmin = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == currentUserContext.UserId
                     && (x.IsAdmin || x.Id == User.DefaultUserId),
                cancellationToken);

        if (!isAdmin)
        {
            throw new ForbiddenException("Admin permissions are required.");
        }
    }

    private static bool HasConfiguredPin(User user)
    {
        return user.Id != User.DefaultUserId
               && !string.IsNullOrWhiteSpace(user.PasswordHash)
               && user.PasswordHash.StartsWith("PBKDF2-SHA256:", StringComparison.Ordinal);
    }
}
