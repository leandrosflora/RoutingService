namespace RoutingService.Domain;

public sealed class NetworkVersion
{
    public string Region { get; private set; } = default!;
    public long Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private NetworkVersion()
    {
    }

    public NetworkVersion(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required", nameof(region));

        Region = region.Trim();
        Version = 1;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Increment()
    {
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
