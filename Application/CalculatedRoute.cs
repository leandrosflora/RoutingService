using RoutingService.Graph;

namespace RoutingService.Application;

public sealed record CalculatedRoute(
    Guid DestinationNodeId,
    DateTimeOffset RequestedAt,
    DateTimeOffset DepartureAt,
    DateTimeOffset ArrivalAt,
    IReadOnlyList<CalculatedRouteLeg> Legs
);

public sealed record CalculatedRouteLeg(
    GraphEdge Edge,
    GraphNode Origin,
    GraphNode Destination,
    DateTimeOffset DepartureAt,
    DateTimeOffset ArrivalAt
);
