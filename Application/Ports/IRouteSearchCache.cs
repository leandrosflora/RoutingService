using RoutingService.Contracts;

namespace RoutingService.Application.Ports;

public interface IRouteSearchCache
{
    Task<SearchRoutesResponse?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(
        string key,
        SearchRoutesResponse response,
        TimeSpan ttl,
        CancellationToken cancellationToken);
}
