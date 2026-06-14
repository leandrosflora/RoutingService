namespace RoutingService.Contracts.Events;

public sealed record ShippingQuoteRequestedPayload(
    Guid? CheckoutId,
    Guid OriginNodeId,
    string DestinationPostalCode,
    PackageProfileDto Package,
    DateTimeOffset RequestedAtUtc,
    int MaxOptions,
    long NetworkVersion,
    IReadOnlyList<RouteOptionResponse> Routes);
