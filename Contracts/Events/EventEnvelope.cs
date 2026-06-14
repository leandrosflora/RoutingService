namespace RoutingService.Contracts.Events;

public sealed record EventEnvelope<TPayload>(
    Guid EventId,
    string EventType,
    string SchemaVersion,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string Producer,
    TPayload Payload);
