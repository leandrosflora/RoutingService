using Microsoft.Extensions.Logging.Abstractions;
using RoutingService.Application;
using RoutingService.Application.Ports;
using RoutingService.Contracts;
using RoutingService.Contracts.Events;
using RoutingService.Domain;
using RoutingService.Graph;

namespace RoutingService.UnitTests;

public sealed class RouteSearchServiceTests
{
    private static readonly Guid OriginId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DestinationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset RequestedAt = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchAsync_CalculatesCachesAndPublishesContractEnvelope()
    {
        var cache = new FakeRouteSearchCache();
        var publisher = new CapturingEventPublisher();
        var service = CreateService(cache, publisher);
        var checkoutId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var request = CreateRequest(MaxOptions: 10);

        var response = await service.SearchAsync(request, " corr-123 ", checkoutId, CancellationToken.None);

        Assert.Equal(42, response.NetworkVersion);
        Assert.Equal("Calculated", response.Source);
        var route = Assert.Single(response.Routes);
        Assert.Equal(OriginId, route.OriginNodeId);
        Assert.Equal(DestinationId, route.DestinationNodeId);
        Assert.Equal(95, route.TotalElapsedMinutes);
        Assert.NotNull(cache.StoredResponse);
        Assert.Equal(TimeSpan.FromMinutes(1), cache.StoredTtl);

        var published = Assert.IsType<PublishedMessage<ShippingQuoteRequestedPayload>>(Assert.Single(publisher.Messages));
        Assert.Equal("checkout.shipping.quote.requested", published.Envelope.EventType);
        Assert.Equal("1.0", published.Envelope.SchemaVersion);
        Assert.Equal("corr-123", published.Envelope.CorrelationId);
        Assert.Equal("checkout-service", published.Envelope.Producer);
        Assert.Equal(checkoutId.ToString("N"), published.MessageKey);
        Assert.Equal(checkoutId, published.Envelope.Payload.CheckoutId);
        Assert.Equal(5, published.Envelope.Payload.MaxOptions);
        Assert.Equal(response.Routes, published.Envelope.Payload.Routes);
    }

    [Fact]
    public async Task SearchAsync_ReturnsCachedResponseAndStillPublishesRequestedEvent()
    {
        var cached = new SearchRoutesResponse(42, "Calculated", Array.Empty<RouteOptionResponse>());
        var cache = new FakeRouteSearchCache { ResponseToReturn = cached };
        var publisher = new CapturingEventPublisher();
        var service = CreateService(cache, publisher);

        var response = await service.SearchAsync(CreateRequest(), null, null, CancellationToken.None);

        Assert.Equal("Cache", response.Source);
        Assert.Null(cache.StoredResponse);
        var published = Assert.IsType<PublishedMessage<ShippingQuoteRequestedPayload>>(Assert.Single(publisher.Messages));
        Assert.Equal("12345678", published.MessageKey);
        Assert.False(string.IsNullOrWhiteSpace(published.Envelope.CorrelationId));
    }

    [Fact]
    public async Task SearchAsync_RejectsInvalidRequestWithoutPublishingOrCaching()
    {
        var cache = new FakeRouteSearchCache();
        var publisher = new CapturingEventPublisher();
        var service = CreateService(cache, publisher);
        var invalid = CreateRequest(WeightKg: 0);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(invalid, CancellationToken.None));

        Assert.Empty(publisher.Messages);
        Assert.Null(cache.StoredResponse);
    }

    private static RouteSearchService CreateService(FakeRouteSearchCache cache, CapturingEventPublisher publisher)
    {
        var store = new RouteGraphStore();
        store.Replace(CreateSnapshot());

        return new RouteSearchService(
            store,
            new TimeDependentRouteEngine(),
            cache,
            publisher,
            NullLogger<RouteSearchService>.Instance);
    }

    private static SearchRoutesRequest CreateRequest(decimal WeightKg = 2, int MaxOptions = 3) => new(
        OriginId,
        "12345-678",
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

    private sealed class CapturingEventPublisher : IEventPublisher
    {
        public List<object> Messages { get; } = [];

        public Task PublishAsync<TPayload>(EventEnvelope<TPayload> envelope, string messageKey, CancellationToken cancellationToken)
        {
            Messages.Add(new PublishedMessage<TPayload>(envelope, messageKey));
            return Task.CompletedTask;
        }
    }

    private sealed record PublishedMessage<TPayload>(EventEnvelope<TPayload> Envelope, string MessageKey);
}
