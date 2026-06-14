using RoutingService.Contracts.Events;

namespace RoutingService.Application.Ports;

public interface IShippingPromiseStore
{
    Task<bool> TryRegisterAsync(Guid eventId, string correlationId, Guid checkoutId, ShippingPromiseCalculatedPayload payload, CancellationToken cancellationToken);
}
