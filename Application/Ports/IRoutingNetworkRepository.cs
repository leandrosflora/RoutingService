using RoutingService.Contracts;
using RoutingService.Domain;
using RoutingService.Graph;

namespace RoutingService.Application.Ports;

public interface IRoutingNetworkRepository
{
    Task<long> GetCurrentVersionAsync(string region, CancellationToken cancellationToken);

    Task<NetworkVersion> GetNetworkVersionAsync(string region, CancellationToken cancellationToken);

    Task<RouteGraphSnapshot> LoadSnapshotAsync(string region, CancellationToken cancellationToken);

    Task<LogisticsNode> CreateNodeAsync(CreateNodeRequest request, CancellationToken cancellationToken);

    Task<LogisticsLane> CreateLaneAsync(CreateLaneRequest request, CancellationToken cancellationToken);

    Task<LogisticsLane> UpdateLaneAsync(Guid laneId, UpdateLaneRequest request, CancellationToken cancellationToken);

    Task<LogisticsLane> ChangeLaneStatusAsync(Guid laneId, ChangeLaneStatusRequest request, CancellationToken cancellationToken);
}
