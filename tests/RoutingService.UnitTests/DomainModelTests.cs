using RoutingService.Domain;

namespace RoutingService.UnitTests;

public sealed class DomainModelTests
{
    [Fact]
    public void LogisticsNode_NormalizesTextAndStartsActive()
    {
        var node = new LogisticsNode(
            " sp01 ",
            " Fulfillment São Paulo ",
            " Brasil Sudeste ",
            " America/Sao_Paulo ",
            LogisticsNodeType.FulfillmentCenter,
            handlingMinutes: 15);

        Assert.Equal("SP01", node.Code);
        Assert.Equal("Fulfillment São Paulo", node.Name);
        Assert.Equal("Brasil Sudeste", node.Region);
        Assert.Equal("America/Sao_Paulo", node.TimeZoneId);
        Assert.True(node.IsActive);
    }

    [Fact]
    public void LogisticsLane_SupportsOnlyActiveCompatiblePackages()
    {
        var lane = new LogisticsLane(
            Guid.NewGuid(),
            Guid.NewGuid(),
            " mel ",
            "standard",
            TransportMode.Road,
            transitMinutes: 120,
            maximumWeightKg: 10,
            maximumCubicWeightKg: 20,
            supportsFragileItems: true,
            supportsRestrictedItems: false);

        Assert.Equal("MEL", lane.CarrierCode);
        Assert.True(lane.Supports(10, 20, isFragile: true, isRestricted: false));
        Assert.False(lane.Supports(10.1m, 20, isFragile: true, isRestricted: false));
        Assert.False(lane.Supports(10, 20, isFragile: true, isRestricted: true));

        lane.ChangeStatus(LaneStatus.Suspended);

        Assert.False(lane.Supports(1, 1, isFragile: false, isRestricted: false));
    }

    [Fact]
    public void PostalCoverage_RejectsInvalidRanges()
    {
        var destinationNodeId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new PostalCoverage(destinationNodeId, 200, 100, 1));
        Assert.Throws<ArgumentException>(() => new PostalCoverage(destinationNodeId, 100, 200, -1));
    }
}
