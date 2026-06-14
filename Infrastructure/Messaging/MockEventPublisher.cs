using RoutingService.Application.Ports;
using RoutingService.Contracts.Events;

namespace RoutingService.Infrastructure.Messaging;

public sealed class MockEventPublisher(ILogger<MockEventPublisher> logger) : IEventPublisher
{
    public Task PublishAsync<TPayload>(EventEnvelope<TPayload> envelope, string messageKey, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Mock Kafka publish topic={Topic} key={MessageKey} eventType={EventType} correlationId={CorrelationId}",
            ResolveTopic(envelope.EventType),
            messageKey,
            envelope.EventType,
            envelope.CorrelationId);

        return Task.CompletedTask;
    }

    private static string ResolveTopic(string eventType) => eventType switch
    {
        "checkout.shipping.quote.requested" => "checkout.shipping.quote.requested",
        _ => eventType
    };
}
