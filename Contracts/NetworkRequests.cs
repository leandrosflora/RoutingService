using RoutingService.Domain;

namespace RoutingService.Contracts;

public sealed record CreateNodeRequest(
    string Code,
    string Name,
    string Region,
    string TimeZoneId,
    LogisticsNodeType Type,
    int HandlingMinutes
);

public sealed record LaneScheduleDto(
    DayOfWeek DayOfWeek,
    TimeOnly DepartureTime,
    bool IsActive = true
);

public sealed record CreateLaneRequest(
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
    string Region,
    IReadOnlyList<LaneScheduleDto> Schedules
);

public sealed record UpdateLaneRequest(
    int TransitMinutes,
    decimal MaximumWeightKg,
    decimal MaximumCubicWeightKg,
    bool SupportsFragileItems,
    bool SupportsRestrictedItems,
    string Region,
    IReadOnlyList<LaneScheduleDto> Schedules
);

public sealed record ChangeLaneStatusRequest(
    LaneStatus Status,
    string Region
);

public sealed record NetworkVersionResponse(
    string Region,
    long Version,
    DateTimeOffset UpdatedAt,
    bool IsGraphLoaded,
    DateTimeOffset? LoadedAt
);
