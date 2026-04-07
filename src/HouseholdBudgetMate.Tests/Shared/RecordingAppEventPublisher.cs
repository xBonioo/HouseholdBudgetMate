using HouseholdBudgetMate.Abstractions.Interfaces;

namespace HouseholdBudgetMate.Tests.Shared;

public sealed class RecordingAppEventPublisher : IAppEventPublisher
{
    private readonly List<object> _events = [];

    public IReadOnlyList<object> Events => _events;

    public Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken)
    {
        _events.Add(appEvent!);
        return Task.CompletedTask;
    }
}