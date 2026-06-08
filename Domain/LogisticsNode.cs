namespace RoutingService.Domain;

public sealed class LogisticsNode
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Region { get; private set; } = default!;
    public string TimeZoneId { get; private set; } = default!;
    public LogisticsNodeType Type { get; private set; }
    public int HandlingMinutes { get; private set; }
    public bool IsActive { get; private set; }

    private LogisticsNode()
    {
    }

    public LogisticsNode(
        string code,
        string name,
        string region,
        string timeZoneId,
        LogisticsNodeType type,
        int handlingMinutes)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required", nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required", nameof(region));

        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ArgumentException("Time zone is required", nameof(timeZoneId));

        if (handlingMinutes < 0)
            throw new ArgumentException("Handling time cannot be negative", nameof(handlingMinutes));

        Id = Guid.NewGuid();
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Region = region.Trim();
        TimeZoneId = timeZoneId.Trim();
        Type = type;
        HandlingMinutes = handlingMinutes;
        IsActive = true;
    }
}
