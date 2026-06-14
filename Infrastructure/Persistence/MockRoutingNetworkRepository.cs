using RoutingService.Application.Ports;
using RoutingService.Contracts;
using RoutingService.Domain;
using RoutingService.Graph;

namespace RoutingService.Infrastructure.Persistence;

public sealed class MockRoutingNetworkRepository : IRoutingNetworkRepository
{
    public const long MockNetworkVersion = 1;
    public const string MockRegion = "Brasil Sudeste";

    private static readonly Guid SaoPauloFulfillmentNodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CampinasHubNodeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RioDeJaneiroDeliveryNodeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BeloHorizonteDeliveryNodeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SaoPauloToCampinasLaneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CampinasToRioLaneId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CampinasToBeloHorizonteLaneId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public Task<long> GetCurrentVersionAsync(string region, CancellationToken cancellationToken)
    {
        return Task.FromResult(MockNetworkVersion);
    }

    public Task<NetworkVersion> GetNetworkVersionAsync(string region, CancellationToken cancellationToken)
    {
        return Task.FromResult(new NetworkVersion(NormalizeRegion(region)));
    }

    public Task<RouteGraphSnapshot> LoadSnapshotAsync(string region, CancellationToken cancellationToken)
    {
        var nodes = new Dictionary<Guid, GraphNode>
        {
            [SaoPauloFulfillmentNodeId] = new(
                SaoPauloFulfillmentNodeId,
                "SP-FUL-01",
                NormalizeRegion(region),
                "America/Sao_Paulo",
                30),
            [CampinasHubNodeId] = new(
                CampinasHubNodeId,
                "CPQ-HUB-01",
                NormalizeRegion(region),
                "America/Sao_Paulo",
                20),
            [RioDeJaneiroDeliveryNodeId] = new(
                RioDeJaneiroDeliveryNodeId,
                "RIO-DLV-01",
                NormalizeRegion(region),
                "America/Sao_Paulo",
                15),
            [BeloHorizonteDeliveryNodeId] = new(
                BeloHorizonteDeliveryNodeId,
                "BHZ-DLV-01",
                NormalizeRegion(region),
                "America/Sao_Paulo",
                15)
        };

        var dailyDepartures = Enum.GetValues<DayOfWeek>()
            .Select(day => new WeeklyDeparture(day, new TimeOnly(9, 0)))
            .ToList();

        var edges = new List<GraphEdge>
        {
            new(
                SaoPauloToCampinasLaneId,
                SaoPauloFulfillmentNodeId,
                CampinasHubNodeId,
                "MELI_LOG",
                TransportMode.Road,
                120,
                30,
                30,
                true,
                false,
                dailyDepartures),
            new(
                CampinasToRioLaneId,
                CampinasHubNodeId,
                RioDeJaneiroDeliveryNodeId,
                "MELI_LOG",
                TransportMode.Road,
                360,
                30,
                30,
                true,
                false,
                dailyDepartures),
            new(
                CampinasToBeloHorizonteLaneId,
                CampinasHubNodeId,
                BeloHorizonteDeliveryNodeId,
                "MELI_LOG",
                TransportMode.Road,
                420,
                30,
                30,
                true,
                false,
                dailyDepartures)
        };

        var adjacency = edges
            .GroupBy(edge => edge.OriginNodeId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<GraphEdge>)group.ToList());

        var coverages = new List<CoverageRange>
        {
            new(20000000, 28999999, RioDeJaneiroDeliveryNodeId, 1),
            new(30000000, 39999999, BeloHorizonteDeliveryNodeId, 1)
        };

        return Task.FromResult(new RouteGraphSnapshot(
            MockNetworkVersion,
            DateTimeOffset.UtcNow,
            nodes,
            adjacency,
            coverages));
    }

    public Task<LogisticsNode> CreateNodeAsync(CreateNodeRequest request, CancellationToken cancellationToken)
    {
        var node = new LogisticsNode(
            request.Code,
            request.Name,
            request.Region,
            request.TimeZoneId,
            request.Type,
            request.HandlingMinutes);

        return Task.FromResult(node);
    }

    public Task<LogisticsLane> CreateLaneAsync(CreateLaneRequest request, CancellationToken cancellationToken)
    {
        var lane = new LogisticsLane(
            request.OriginNodeId,
            request.DestinationNodeId,
            request.CarrierCode,
            request.Mode,
            request.TransitMinutes,
            request.MaximumWeightKg,
            request.MaximumCubicWeightKg,
            request.SupportsFragileItems,
            request.SupportsRestrictedItems,
            request.Schedules.Select(ToSchedule));

        return Task.FromResult(lane);
    }

    public Task<LogisticsLane> UpdateLaneAsync(
        Guid laneId,
        UpdateLaneRequest request,
        CancellationToken cancellationToken)
    {
        var lane = new LogisticsLane(
            SaoPauloFulfillmentNodeId,
            CampinasHubNodeId,
            "MELI_LOG",
            TransportMode.Road,
            request.TransitMinutes,
            request.MaximumWeightKg,
            request.MaximumCubicWeightKg,
            request.SupportsFragileItems,
            request.SupportsRestrictedItems,
            request.Schedules.Select(ToSchedule));

        return Task.FromResult(lane);
    }

    public Task<LogisticsLane> ChangeLaneStatusAsync(
        Guid laneId,
        ChangeLaneStatusRequest request,
        CancellationToken cancellationToken)
    {
        var lane = new LogisticsLane(
            SaoPauloFulfillmentNodeId,
            CampinasHubNodeId,
            "MELI_LOG",
            TransportMode.Road,
            120,
            30,
            30,
            true,
            false,
            [new LaneSchedule(DayOfWeek.Monday, new TimeOnly(9, 0))]);

        lane.ChangeStatus(request.Status);
        return Task.FromResult(lane);
    }

    private static string NormalizeRegion(string region)
    {
        return string.IsNullOrWhiteSpace(region) ? MockRegion : region.Trim();
    }

    private static LaneSchedule ToSchedule(LaneScheduleDto schedule)
    {
        return new LaneSchedule(schedule.DayOfWeek, schedule.DepartureTime, schedule.IsActive);
    }
}
