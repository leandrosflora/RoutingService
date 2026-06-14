using RoutingService.Contracts.Events;

namespace RoutingService.Application.Ports;

public interface IEventPublisher
{
    Task PublishAsync<TPayload>(EventEnvelope<TPayload> envelope, string messageKey, CancellationToken cancellationToken);
}
