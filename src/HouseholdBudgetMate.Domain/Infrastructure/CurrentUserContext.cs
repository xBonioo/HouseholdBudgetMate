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

    private sealed class ScopeReset(Action reset) : IDisposable
    {
        private Action? _reset = reset;

        public void Dispose()
        {
            Interlocked.Exchange(ref _reset, null)?.Invoke();
        }
    }
}
