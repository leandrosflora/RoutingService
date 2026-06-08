namespace RoutingService.Contracts;

public sealed record SearchRoutesRequest(
    Guid OriginNodeId,
    string DestinationPostalCode,
    PackageProfileDto Package,
    DateTimeOffset? RequestedAtUtc = null,
    int MaxOptions = 3
);

public sealed record PackageProfileDto(
    decimal WeightKg,
    decimal CubicWeightKg,
    bool IsFragile,
    bool IsRestricted
);
