using RoutingService.Contracts;

namespace RoutingService.Application.Ports;

public interface ICalculatedRouteStore
{
    Task SaveAsync(RouteOptionResponse route, CancellationToken cancellationToken);

    Task<RouteOptionResponse?> GetAsync(string routeId, CancellationToken cancellationToken);
}
