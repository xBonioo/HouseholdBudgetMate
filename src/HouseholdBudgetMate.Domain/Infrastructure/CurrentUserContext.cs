using HouseholdBudgetMate.Domain.Entities;

namespace HouseholdBudgetMate.Domain.Infrastructure;

public sealed class CurrentUserContext
{
    public string UserId { get; set; } = string.Empty;
    public string? BudgetOwnerUserId { get; set; }
    public bool IsSystemOperation { get; private set; }

    public static CurrentUserContext ForTechnicalOwner()
    {
        return new CurrentUserContext
        {
            UserId = User.DefaultUserId,
            BudgetOwnerUserId = User.DefaultUserId,
            IsSystemOperation = true
        };
    }

    public IDisposable BeginTechnicalOwnerScope()
    {
        var previousUserId = UserId;
        var previousBudgetOwnerUserId = BudgetOwnerUserId;
        var previousIsSystemOperation = IsSystemOperation;

        UserId = User.DefaultUserId;
        BudgetOwnerUserId = User.DefaultUserId;
        IsSystemOperation = true;

        return new ScopeReset(() =>
        {
            UserId = previousUserId;
            BudgetOwnerUserId = previousBudgetOwnerUserId;
            IsSystemOperation = previousIsSystemOperation;
        });
    }

    public void SetInteractiveUser(string userId, string budgetOwnerUserId)
    {
        if (string.IsNullOrWhiteSpace(userId)
            || userId == User.DefaultUserId
            || string.IsNullOrWhiteSpace(budgetOwnerUserId))
        {
            throw new ArgumentException("A visible user and budget owner are required for interactive access.");
        }

        UserId = userId;
        BudgetOwnerUserId = budgetOwnerUserId;
        IsSystemOperation = false;
    }

    public void ClearInteractiveUser()
    {
        UserId = string.Empty;
        BudgetOwnerUserId = null;
        IsSystemOperation = false;
    }

    private sealed class ScopeReset(Action reset) : IDisposable
    {
        private Action? _reset = reset;

        public void Dispose()
        {
            Interlocked.Exchange(ref _reset, null)?.Invoke();
        }
    }
}
