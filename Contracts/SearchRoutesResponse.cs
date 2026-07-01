using RoutingService.Domain;

namespace RoutingService.Contracts;

public sealed record SearchRoutesResponse(
    long NetworkVersion,
    string Source,
    IReadOnlyList<RouteOptionResponse> Routes
);

public sealed record RouteOptionResponse(
    string RouteId,
    Guid OriginNodeId,
    Guid DestinationNodeId,
    DateTimeOffset EstimatedDepartureAt,
    DateTimeOffset EstimatedArrivalAt,
    int TotalElapsedMinutes,
    IReadOnlyList<RouteLegResponse> Legs
);

public sealed record RouteLegResponse(
    Guid LaneId,
    Guid OriginNodeId,
    string OriginCode,
    Guid DestinationNodeId,
    string DestinationCode,
    string CarrierCode,
    string ServiceLevelCode,
    TransportMode Mode,
    DateTimeOffset DepartureAt,
    DateTimeOffset ArrivalAt,
    int TransitMinutes
);
