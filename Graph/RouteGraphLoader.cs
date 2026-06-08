using RoutingService.Application.Ports;

namespace RoutingService.Graph;

public sealed class RouteGraphLoader
{
    private readonly IRoutingNetworkRepository _repository;
    private readonly RouteGraphStore _store;
    private readonly IConfiguration _configuration;

    public RouteGraphLoader(
        IRoutingNetworkRepository repository,
        RouteGraphStore store,
        IConfiguration configuration)
    {
        _repository = repository;
        _store = store;
        _configuration = configuration;
    }

    public async Task<RouteGraphSnapshot> ReloadAsync(CancellationToken cancellationToken)
    {
        var region = _configuration["Routing:Region"] ?? "Brasil Sudeste";
        var snapshot = await _repository.LoadSnapshotAsync(region, cancellationToken);
        _store.Replace(snapshot);
        return snapshot;
    }
}
