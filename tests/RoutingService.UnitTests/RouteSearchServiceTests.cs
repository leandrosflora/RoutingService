using Microsoft.Extensions.Logging.Abstractions;
using RoutingService.Application;
using RoutingService.Application.Ports;
using RoutingService.Contracts;
using RoutingService.Domain;
using RoutingService.Graph;

namespace RoutingService.UnitTests;

public sealed class RouteSearchServiceTests
{
    private static readonly Guid OriginId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DestinationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset RequestedAt = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchAsync_CalculatesCachesAndStoresRouteDetails()
    {
        var cache = new FakeRouteSearchCache();
        var routeStore = new FakeCalculatedRouteStore();
        var service = CreateService(cache, routeStore);
        var request = CreateRequest(MaxOptions: 10);

        var response = await service.SearchAsync(request, " corr-123 ", CancellationToken.None);

        Assert.Equal(42, response.NetworkVersion);
        Assert.Equal("Calculated", response.Source);
        var route = Assert.Single(response.Routes);
        Assert.Equal(OriginId, route.OriginNodeId);
        Assert.Equal(DestinationId, route.DestinationNodeId);
        Assert.Equal(95, route.TotalElapsedMinutes);
        Assert.NotNull(cache.StoredResponse);
        Assert.Equal(TimeSpan.FromMinutes(1), cache.StoredTtl);
        Assert.Same(route, routeStore.SavedRoutes[route.RouteId]);
    }

    [Fact]
    public async Task SearchAsync_ReturnsCachedResponseAndStoresRouteDetails()
    {
        var route = new RouteOptionResponse(
            "route_cached",
            OriginId,
            DestinationId,
            RequestedAt,
            RequestedAt.AddMinutes(90),
            90,
            []);
        var cached = new SearchRoutesResponse(42, "Calculated", [route]);
        var cache = new FakeRouteSearchCache { ResponseToReturn = cached };
        var routeStore = new FakeCalculatedRouteStore();
        var service = CreateService(cache, routeStore);

        var response = await service.SearchAsync(CreateRequest(), null, CancellationToken.None);

        Assert.Equal("Cache", response.Source);
        Assert.Null(cache.StoredResponse);
        Assert.Same(route, routeStore.SavedRoutes[route.RouteId]);
    }


    [Fact]
    public async Task SearchAsync_ReturnsNoRoutesWhenPostalCodeIsOutsideCoverage()
    {
        var cache = new FakeRouteSearchCache();
        var routeStore = new FakeCalculatedRouteStore();
        var service = CreateService(cache, routeStore);
        var request = CreateRequest(DestinationPostalCode: "05700-000");

        var response = await service.SearchAsync(request, CancellationToken.None);

        Assert.Equal(42, response.NetworkVersion);
        Assert.Equal("Calculated", response.Source);
        Assert.Empty(response.Routes);
        Assert.NotNull(cache.StoredResponse);
        Assert.Empty(routeStore.SavedRoutes);
    }

    [Fact]
    public async Task SearchAsync_RejectsInvalidRequestWithoutStoringRoute()
    {
        var cache = new FakeRouteSearchCache();
        var routeStore = new FakeCalculatedRouteStore();
        var service = CreateService(cache, routeStore);
        var invalid = CreateRequest(WeightKg: 0);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(invalid, CancellationToken.None));

        Assert.Empty(routeStore.SavedRoutes);
        Assert.Null(cache.StoredResponse);
    }

    [Fact]
    public async Task GetRouteAsync_ReturnsStoredRouteDetails()
    {
        var cache = new FakeRouteSearchCache();
        var routeStore = new FakeCalculatedRouteStore();
        var service = CreateService(cache, routeStore);
        var response = await service.SearchAsync(CreateRequest(), CancellationToken.None);
        var route = Assert.Single(response.Routes);

        var storedRoute = await service.GetRouteAsync(route.RouteId, CancellationToken.None);

        Assert.Equal(route, storedRoute);
    }

    private static RouteSearchService CreateService(FakeRouteSearchCache cache, FakeCalculatedRouteStore routeStore)
    {
        var store = new RouteGraphStore();
        store.Replace(CreateSnapshot());

        return new RouteSearchService(
            store,
            new TimeDependentRouteEngine(),
            cache,
            routeStore,
            NullLogger<RouteSearchService>.Instance);
    }

    private static SearchRoutesRequest CreateRequest(
        decimal WeightKg = 2,
        int MaxOptions = 3,
        string DestinationPostalCode = "12345-678") => new(
        OriginId,
        DestinationPostalCode,
        new PackageProfileDto(WeightKg, CubicWeightKg: 1, IsFragile: false, IsRestricted: false),
        RequestedAt,
        MaxOptions);

    private static RouteGraphSnapshot CreateSnapshot()
    {
        var nodes = new Dictionary<Guid, GraphNode>
        {
            [OriginId] = new(OriginId, "ORI", "Brasil Sudeste", "UTC", 0),
            [DestinationId] = new(DestinationId, "DST", "Brasil Sudeste", "UTC", 5)
        };

        var adjacency = new Dictionary<Guid, IReadOnlyList<GraphEdge>>
        {
            [OriginId] = new[]
            {
                new GraphEdge(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), OriginId, DestinationId, "MEL", TransportMode.Road, 90, 30, 30, true, true,
                    new[] { new WeeklyDeparture(DayOfWeek.Monday, new TimeOnly(10, 0)) })
            }
        };

        var coverages = new[] { new CoverageRange(12345000, 12345999, DestinationId, 1) };
        return new RouteGraphSnapshot(42, DateTimeOffset.UtcNow, nodes, adjacency, coverages);
    }

    private sealed class FakeRouteSearchCache : IRouteSearchCache
    {
        public SearchRoutesResponse? ResponseToReturn { get; init; }
        public SearchRoutesResponse? StoredResponse { get; private set; }
        public TimeSpan? StoredTtl { get; private set; }

        public Task<SearchRoutesResponse?> GetAsync(string key, CancellationToken cancellationToken) => Task.FromResult(ResponseToReturn);

        public Task SetAsync(string key, SearchRoutesResponse response, TimeSpan ttl, CancellationToken cancellationToken)
        {
            StoredResponse = response;
            StoredTtl = ttl;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCalculatedRouteStore : ICalculatedRouteStore
    {
        public Dictionary<string, RouteOptionResponse> SavedRoutes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task SaveAsync(RouteOptionResponse route, CancellationToken cancellationToken)
        {
            SavedRoutes[route.RouteId] = route;
            return Task.CompletedTask;
        }

        public Task<RouteOptionResponse?> GetAsync(string routeId, CancellationToken cancellationToken)
        {
            SavedRoutes.TryGetValue(routeId, out var route);
            return Task.FromResult(route);
        }
    }
}
