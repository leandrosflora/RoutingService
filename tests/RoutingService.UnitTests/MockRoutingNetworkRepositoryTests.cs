using RoutingService.Application;
using RoutingService.Contracts;
using RoutingService.Infrastructure.Persistence;

namespace RoutingService.UnitTests;

public sealed class MockRoutingNetworkRepositoryTests
{
    private static readonly Guid ReportedOriginNodeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SaoPauloCep057DeliveryNodeId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset ReportedRequestedAt = DateTimeOffset.Parse("2026-06-30T21:42:26.418Z");

    [Fact]
    public async Task LoadSnapshotAsync_ReturnsRouteForReportedCep057Payload()
    {
        var repository = new MockRoutingNetworkRepository();
        var graph = await repository.LoadSnapshotAsync(MockRoutingNetworkRepository.MockRegion, CancellationToken.None);
        var destinationNodeIds = graph.Coverages
            .Where(x => 5700000 >= x.PostalCodeFrom && 5700000 <= x.PostalCodeTo)
            .Select(x => x.DestinationNodeId)
            .ToHashSet();
        var package = new PackageProfileDto(
            WeightKg: 0.450m,
            CubicWeightKg: 0.4693333333333333333333333333m,
            IsFragile: false,
            IsRestricted: false);

        var routes = new TimeDependentRouteEngine().Search(
            graph,
            ReportedOriginNodeId,
            destinationNodeIds,
            package,
            ReportedRequestedAt,
            maxOptions: 3);

        var route = Assert.Single(routes);
        Assert.Equal(SaoPauloCep057DeliveryNodeId, route.DestinationNodeId);
        Assert.Equal(new DateTimeOffset(2026, 6, 30, 22, 0, 0, TimeSpan.Zero), route.DepartureAt);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 0, 15, 0, TimeSpan.Zero), route.ArrivalAt);
    }
}
