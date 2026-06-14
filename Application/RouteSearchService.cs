using System.Security.Cryptography;
using System.Text;
using RoutingService.Application.Ports;
using RoutingService.Contracts;
using RoutingService.Contracts.Events;
using RoutingService.Graph;

namespace RoutingService.Application;

public sealed class RouteSearchService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    private readonly RouteGraphStore _graphStore;
    private readonly TimeDependentRouteEngine _routeEngine;
    private readonly IRouteSearchCache _cache;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<RouteSearchService> _logger;

    public RouteSearchService(
        RouteGraphStore graphStore,
        TimeDependentRouteEngine routeEngine,
        IRouteSearchCache cache,
        IEventPublisher eventPublisher,
        ILogger<RouteSearchService> logger)
    {
        _graphStore = graphStore;
        _routeEngine = routeEngine;
        _cache = cache;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<SearchRoutesResponse> SearchAsync(
        SearchRoutesRequest request,
        CancellationToken cancellationToken)
    {
        return await SearchAsync(request, null, null, cancellationToken);
    }

    public async Task<SearchRoutesResponse> SearchAsync(
        SearchRoutesRequest request,
        string? correlationId,
        Guid? checkoutId,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var requestedAt = (request.RequestedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var graph = _graphStore.Current;
        var cacheKey = RouteCacheKeyFactory.Build(graph.Version, request, requestedAt);
        var cached = await _cache.GetAsync(cacheKey, cancellationToken);

        if (cached is not null)
        {
            var cachedResponse = cached with { Source = "Cache" };
            await PublishShippingQuoteRequestedAsync(request, requestedAt, graph.Version, cachedResponse, correlationId, checkoutId, cancellationToken);
            return cachedResponse;
        }

        var postalCode = NormalizePostalCode(request.DestinationPostalCode);
        var destinationNodeIds = graph.Coverages
            .Where(x => postalCode >= x.PostalCodeFrom && postalCode <= x.PostalCodeTo)
            .OrderBy(x => x.Priority)
            .Select(x => x.DestinationNodeId)
            .ToHashSet();

        if (destinationNodeIds.Count == 0)
        {
            var empty = new SearchRoutesResponse(graph.Version, "Calculated", []);
            await _cache.SetAsync(cacheKey, empty, CacheTtl, cancellationToken);
            await PublishShippingQuoteRequestedAsync(request, requestedAt, graph.Version, empty, correlationId, checkoutId, cancellationToken);
            return empty;
        }

        var calculatedRoutes = _routeEngine.Search(
            graph,
            request.OriginNodeId,
            destinationNodeIds,
            request.Package,
            requestedAt,
            Math.Clamp(request.MaxOptions, 1, 5));

        var response = new SearchRoutesResponse(
            graph.Version,
            "Calculated",
            calculatedRoutes.Select(x => Map(graph.Version, x)).ToList());

        await _cache.SetAsync(cacheKey, response, CacheTtl, cancellationToken);
        await PublishShippingQuoteRequestedAsync(request, requestedAt, graph.Version, response, correlationId, checkoutId, cancellationToken);
        return response;
    }


    private async Task PublishShippingQuoteRequestedAsync(
        SearchRoutesRequest request,
        DateTimeOffset requestedAt,
        long networkVersion,
        SearchRoutesResponse response,
        string? correlationId,
        Guid? checkoutId,
        CancellationToken cancellationToken)
    {
        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId.Trim();

        var payload = new ShippingQuoteRequestedPayload(
            checkoutId,
            request.OriginNodeId,
            request.DestinationPostalCode,
            request.Package,
            requestedAt,
            Math.Clamp(request.MaxOptions, 1, 5),
            networkVersion,
            response.Routes);

        var envelope = new EventEnvelope<ShippingQuoteRequestedPayload>(
            Guid.NewGuid(),
            "checkout.shipping.quote.requested",
            "1.0",
            DateTimeOffset.UtcNow,
            resolvedCorrelationId,
            "checkout-service",
            payload);

        var messageKey = checkoutId?.ToString("N") ?? request.DestinationPostalCode;
        await _eventPublisher.PublishAsync(envelope, messageKey, cancellationToken);

        _logger.LogInformation(
            "Shipping quote requested event emitted key={MessageKey} correlationId={CorrelationId} checkoutId={CheckoutId}",
            messageKey,
            resolvedCorrelationId,
            checkoutId);
    }

    private static RouteOptionResponse Map(long networkVersion, CalculatedRoute route)
    {
        return new RouteOptionResponse(
            CreateRouteId(networkVersion, route),
            route.Legs[0].Origin.Id,
            route.DestinationNodeId,
            route.DepartureAt,
            route.ArrivalAt,
            (int)(route.ArrivalAt - route.RequestedAt).TotalMinutes,
            route.Legs.Select(x => new RouteLegResponse(
                x.Edge.LaneId,
                x.Origin.Id,
                x.Origin.Code,
                x.Destination.Id,
                x.Destination.Code,
                x.Edge.CarrierCode,
                x.Edge.Mode,
                x.DepartureAt,
                x.ArrivalAt,
                x.Edge.TransitMinutes)).ToList());
    }

    private static string CreateRouteId(long networkVersion, CalculatedRoute route)
    {
        var raw = string.Join(
            ":",
            networkVersion,
            route.Legs.Select(x => $"{x.Edge.LaneId}-{x.DepartureAt:O}"));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"route_{Convert.ToHexString(hash)[..24].ToLowerInvariant()}";
    }

    private static long NormalizePostalCode(string postalCode)
    {
        var digits = new string(postalCode.Where(char.IsDigit).ToArray());

        if (digits.Length != 8 || !long.TryParse(digits, out var result))
            throw new ArgumentException("Destination postal code is invalid", nameof(postalCode));

        return result;
    }

    private static void Validate(SearchRoutesRequest request)
    {
        if (request.OriginNodeId == Guid.Empty)
            throw new ArgumentException("OriginNodeId is required", nameof(request));

        if (request.Package is null)
            throw new ArgumentException("Package is required", nameof(request));

        if (request.Package.WeightKg <= 0)
            throw new ArgumentException("Weight must be positive", nameof(request));

        if (request.Package.CubicWeightKg < 0)
            throw new ArgumentException("Cubic weight cannot be negative", nameof(request));
    }
}
