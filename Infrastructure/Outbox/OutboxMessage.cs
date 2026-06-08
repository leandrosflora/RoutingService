namespace RoutingService.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private OutboxMessage()
    {
    }

    public OutboxMessage(string type, string payload)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Outbox message type is required", nameof(type));

        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Outbox payload is required", nameof(payload));

        Id = Guid.NewGuid();
        Type = type.Trim();
        Payload = payload;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public void MarkProcessed()
    {
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
