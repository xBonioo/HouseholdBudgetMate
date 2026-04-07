using HouseholdBudgetMate.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace HouseholdBudgetMate.Application.Helpers;

public sealed class LoggingAppEventPublisher(ILogger<LoggingAppEventPublisher> logger) : IAppEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("AppEvent emitted: {EventType} {@EventPayload}", typeof(TEvent).Name, appEvent);
        return Task.CompletedTask;
    }
}