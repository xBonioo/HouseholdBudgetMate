using HouseholdBudgetMate.Abstractions.Contracts.Users.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Users.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto> UpdateUserBudgetModeAsync(UpdateUserBudgetModeRequest request, CancellationToken cancellationToken);
    Task<UserDto> UpdateUserAdminRoleAsync(UpdateUserAdminRoleRequest request, CancellationToken cancellationToken);
    Task UpdateUserPinAsync(UpdateUserPinRequest request, CancellationToken cancellationToken);
    Task<bool> ValidatePinAsync(string userId, string pin, CancellationToken cancellationToken);
}
