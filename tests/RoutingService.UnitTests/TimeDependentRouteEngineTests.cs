using RoutingService.Application;
using RoutingService.Contracts;
using RoutingService.Domain;
using RoutingService.Graph;

namespace RoutingService.UnitTests;

public sealed class TimeDependentRouteEngineTests
{
    private static readonly Guid OriginId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HubId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DestinationId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Search_ReturnsEarliestCompatibleRouteUsingSchedulesAndHandlingTime()
    {
        var requestedAt = new DateTimeOffset(2026, 6, 15, 11, 30, 0, TimeSpan.Zero);
        var graph = CreateGraph(includeFastIncompatibleLane: true);
        var package = new PackageProfileDto(WeightKg: 5, CubicWeightKg: 3, IsFragile: true, IsRestricted: false);

        var routes = new TimeDependentRouteEngine().Search(
            graph,
            OriginId,
            new HashSet<Guid> { DestinationId },
            package,
            requestedAt,
            maxOptions: 3);

        var route = Assert.Single(routes);
        Assert.Equal(DestinationId, route.DestinationNodeId);
        Assert.Equal(requestedAt, route.RequestedAt);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero), route.DepartureAt);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 16, 20, 0, TimeSpan.Zero), route.ArrivalAt);
        Assert.Equal(2, route.Legs.Count);
        Assert.All(route.Legs, leg => Assert.True(leg.Edge.SupportsFragileItems));
    }

    [Fact]
    public void Search_ThrowsWhenOriginDoesNotExist()
    {
        var graph = CreateGraph(includeFastIncompatibleLane: false);
        var package = new PackageProfileDto(1, 1, IsFragile: false, IsRestricted: false);

        Assert.Throws<ArgumentException>(() => new TimeDependentRouteEngine().Search(
            graph,
            Guid.NewGuid(),
            new HashSet<Guid> { DestinationId },
            package,
            DateTimeOffset.UtcNow,
            maxOptions: 1));
    }

    private static RouteGraphSnapshot CreateGraph(bool includeFastIncompatibleLane)
    {
        var nodes = new Dictionary<Guid, GraphNode>
        {
            [OriginId] = new(OriginId, "ORI", "Brasil Sudeste", "UTC", 0),
            [HubId] = new(HubId, "HUB", "Brasil Sudeste", "UTC", 10),
            [DestinationId] = new(DestinationId, "DST", "Brasil Sudeste", "UTC", 30)
        };

        var originEdges = new List<GraphEdge>
        {
            new(Guid.NewGuid(), OriginId, HubId, "MEL", "standard", TransportMode.Road, 120, 30, 30, true, false,
                new[] { new WeeklyDeparture(DayOfWeek.Monday, new TimeOnly(12, 0)) })
        };

        if (includeFastIncompatibleLane)
        {
            originEdges.Add(new GraphEdge(Guid.NewGuid(), OriginId, DestinationId, "MEL", "standard", TransportMode.Air, 60, 30, 30, false, false,
                new[] { new WeeklyDeparture(DayOfWeek.Monday, new TimeOnly(11, 45)) }));
        }

        var adjacency = new Dictionary<Guid, IReadOnlyList<GraphEdge>>
        {
            [OriginId] = originEdges,
            [HubId] = new[]
            {
                new GraphEdge(Guid.NewGuid(), HubId, DestinationId, "MEL", "standard", TransportMode.Road, 100, 30, 30, true, false,
                    new[] { new WeeklyDeparture(DayOfWeek.Monday, new TimeOnly(14, 10)) })
            }
        };

        return new RouteGraphSnapshot(7, DateTimeOffset.UtcNow, nodes, adjacency, Array.Empty<CoverageRange>());
    }
}
