using System.Collections.Concurrent;
using RoutingService.Application.Ports;
using RoutingService.Contracts.Events;

namespace RoutingService.Infrastructure.Messaging;

public sealed class InMemoryShippingPromiseStore(ILogger<InMemoryShippingPromiseStore> logger) : IShippingPromiseStore
{
    private readonly ConcurrentDictionary<string, ShippingPromiseCalculatedPayload> _processed = new();

    public Task<bool> TryRegisterAsync(Guid eventId, string correlationId, Guid checkoutId, ShippingPromiseCalculatedPayload payload, CancellationToken cancellationToken)
    {
        var key = $"{eventId:N}:{correlationId}:{checkoutId:N}";
        var registered = _processed.TryAdd(key, payload);

        logger.LogInformation(
            "Shipping promise {Action} eventId={EventId} correlationId={CorrelationId} checkoutId={CheckoutId}",
            registered ? "registered" : "ignored_duplicate",
            eventId,
            correlationId,
            checkoutId);

        return Task.FromResult(registered);
    }
}
