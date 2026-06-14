namespace RoutingService.Contracts.Events;

public sealed record ShippingPromiseCalculatedPayload(
    Guid CheckoutId,
    string? PromiseId,
    DateTimeOffset? EstimatedDeliveryAt,
    string? Status,
    string? RouteId);
