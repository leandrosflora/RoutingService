using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using RoutingService.Application.Ports;
using RoutingService.Contracts;

namespace RoutingService.Infrastructure.Cache;

public sealed class RedisRouteSearchCache : IRouteSearchCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IDistributedCache _cache;

    public RedisRouteSearchCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<SearchRoutesResponse?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(key, cancellationToken);

        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<SearchRoutesResponse>(json, JsonOptions);
    }

    public Task SetAsync(
        string key,
        SearchRoutesResponse response,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        return _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(response, JsonOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            },
            cancellationToken);
    }
}
