using System.Collections.Concurrent;
using RoutingService.Application.Ports;
using RoutingService.Contracts;

namespace RoutingService.Infrastructure.Persistence;

public sealed class InMemoryCalculatedRouteStore : ICalculatedRouteStore
{
    private readonly ConcurrentDictionary<string, RouteOptionResponse> _routes = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(RouteOptionResponse route, CancellationToken cancellationToken)
    {
        _routes[route.RouteId] = route;
        return Task.CompletedTask;
    }

    public Task<RouteOptionResponse?> GetAsync(string routeId, CancellationToken cancellationToken)
    {
        _routes.TryGetValue(routeId, out var route);
        return Task.FromResult(route);
    }
}
