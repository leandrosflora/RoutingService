namespace RoutingService.Domain;

public sealed class LogisticsLane
{
    public Guid Id { get; private set; }
    public Guid OriginNodeId { get; private set; }
    public Guid DestinationNodeId { get; private set; }
    public string CarrierCode { get; private set; } = default!;
    public string ServiceLevelCode { get; private set; } = default!;
    public TransportMode Mode { get; private set; }
    public int TransitMinutes { get; private set; }
    public decimal MaximumWeightKg { get; private set; }
    public decimal MaximumCubicWeightKg { get; private set; }
    public bool SupportsFragileItems { get; private set; }
    public bool SupportsRestrictedItems { get; private set; }
    public LaneStatus Status { get; private set; }
    public long Version { get; private set; }
    public List<LaneSchedule> Schedules { get; private set; } = [];

    private LogisticsLane()
    {
    }

    public LogisticsLane(
        Guid originNodeId,
        Guid destinationNodeId,
        string carrierCode,
        string serviceLevelCode,
        TransportMode mode,
        int transitMinutes,
        decimal maximumWeightKg,
        decimal maximumCubicWeightKg,
        bool supportsFragileItems,
        bool supportsRestrictedItems,
        IEnumerable<LaneSchedule>? schedules = null)
    {
        Validate(originNodeId, destinationNodeId, carrierCode, serviceLevelCode, transitMinutes, maximumWeightKg, maximumCubicWeightKg);

        Id = Guid.NewGuid();
        OriginNodeId = originNodeId;
        DestinationNodeId = destinationNodeId;
        CarrierCode = carrierCode.Trim().ToUpperInvariant();
        // Not upper-invariant: CarrierService/ShippingPricingService store this as-is
        // (e.g. "same_day", "standard") and compare it verbatim / case-insensitively.
        ServiceLevelCode = serviceLevelCode.Trim();
        Mode = mode;
        TransitMinutes = transitMinutes;
        MaximumWeightKg = maximumWeightKg;
        MaximumCubicWeightKg = maximumCubicWeightKg;
        SupportsFragileItems = supportsFragileItems;
        SupportsRestrictedItems = supportsRestrictedItems;
        Status = LaneStatus.Active;
        Version = 1;
        Schedules = schedules?.ToList() ?? [];
    }

    public bool Supports(decimal weightKg, decimal cubicWeightKg, bool isFragile, bool isRestricted)
    {
        if (Status != LaneStatus.Active)
            return false;

        if (weightKg > MaximumWeightKg)
            return false;

        if (cubicWeightKg > MaximumCubicWeightKg)
            return false;

        if (isFragile && !SupportsFragileItems)
            return false;

        if (isRestricted && !SupportsRestrictedItems)
            return false;

        return true;
    }

    public void Update(
        int transitMinutes,
        decimal maximumWeightKg,
        decimal maximumCubicWeightKg,
        bool supportsFragileItems,
        bool supportsRestrictedItems,
        IEnumerable<LaneSchedule> schedules)
    {
        Validate(OriginNodeId, DestinationNodeId, CarrierCode, ServiceLevelCode, transitMinutes, maximumWeightKg, maximumCubicWeightKg);

        TransitMinutes = transitMinutes;
        MaximumWeightKg = maximumWeightKg;
        MaximumCubicWeightKg = maximumCubicWeightKg;
        SupportsFragileItems = supportsFragileItems;
        SupportsRestrictedItems = supportsRestrictedItems;
        Schedules = schedules.ToList();
        Version++;
    }

    public void ChangeStatus(LaneStatus status)
    {
        Status = status;
        Version++;
    }

    private static void Validate(
        Guid originNodeId,
        Guid destinationNodeId,
        string carrierCode,
        string serviceLevelCode,
        int transitMinutes,
        decimal maximumWeightKg,
        decimal maximumCubicWeightKg)
    {
        if (originNodeId == Guid.Empty)
            throw new ArgumentException("Origin node is required", nameof(originNodeId));

        if (destinationNodeId == Guid.Empty)
            throw new ArgumentException("Destination node is required", nameof(destinationNodeId));

        if (originNodeId == destinationNodeId)
            throw new ArgumentException("Origin and destination must be different", nameof(destinationNodeId));

        if (string.IsNullOrWhiteSpace(carrierCode))
            throw new ArgumentException("Carrier code is required", nameof(carrierCode));

        if (string.IsNullOrWhiteSpace(serviceLevelCode))
            throw new ArgumentException("Service level code is required", nameof(serviceLevelCode));

        if (transitMinutes <= 0)
            throw new ArgumentException("Transit minutes must be positive", nameof(transitMinutes));

        if (maximumWeightKg <= 0)
            throw new ArgumentException("Maximum weight must be positive", nameof(maximumWeightKg));

        if (maximumCubicWeightKg < 0)
            throw new ArgumentException("Maximum cubic weight cannot be negative", nameof(maximumCubicWeightKg));
    }
}
