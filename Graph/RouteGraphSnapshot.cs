using RoutingService.Domain;

namespace RoutingService.Graph;

public sealed record GraphNode(
    Guid Id,
    string Code,
    string Region,
    string TimeZoneId,
    int HandlingMinutes
);

public sealed record WeeklyDeparture(
    DayOfWeek DayOfWeek,
    TimeOnly DepartureTime
);

public sealed record GraphEdge(
    Guid LaneId,
    Guid OriginNodeId,
    Guid DestinationNodeId,
    string CarrierCode,
    string ServiceLevelCode,
    TransportMode Mode,
    int TransitMinutes,
    decimal MaximumWeightKg,
    decimal MaximumCubicWeightKg,
    bool SupportsFragileItems,
    bool SupportsRestrictedItems,
    IReadOnlyList<WeeklyDeparture> Departures
);

public sealed record CoverageRange(
    long PostalCodeFrom,
    long PostalCodeTo,
    Guid DestinationNodeId,
    int Priority
);

public sealed record RouteGraphSnapshot(
    long Version,
    DateTimeOffset LoadedAt,
    IReadOnlyDictionary<Guid, GraphNode> Nodes,
    IReadOnlyDictionary<Guid, IReadOnlyList<GraphEdge>> Adjacency,
    IReadOnlyList<CoverageRange> Coverages
);
