namespace RoutingService.Graph;

public sealed class RouteGraphStore
{
    private RouteGraphSnapshot? _current;

    public RouteGraphSnapshot Current =>
        Volatile.Read(ref _current)
        ?? throw new InvalidOperationException("Routing graph has not been loaded");

    public bool IsLoaded => Volatile.Read(ref _current) is not null;

    public void Replace(RouteGraphSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _current, snapshot);
    }
}
