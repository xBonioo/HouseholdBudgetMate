namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IAppEventPublisher
{
    Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken);
}