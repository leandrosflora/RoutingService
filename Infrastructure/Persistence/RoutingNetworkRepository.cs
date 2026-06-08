using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoutingService.Application.Ports;
using RoutingService.Contracts;
using RoutingService.Domain;
using RoutingService.Graph;
using RoutingService.Infrastructure.Outbox;

namespace RoutingService.Infrastructure.Persistence;

public sealed class RoutingNetworkRepository : IRoutingNetworkRepository
{
    private readonly RoutingDbContext _dbContext;

    public RoutingNetworkRepository(RoutingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<long> GetCurrentVersionAsync(string region, CancellationToken cancellationToken)
    {
        var networkVersion = await EnsureNetworkVersionAsync(region, cancellationToken);
        return networkVersion.Version;
    }

    public Task<NetworkVersion> GetNetworkVersionAsync(string region, CancellationToken cancellationToken)
    {
        return EnsureNetworkVersionAsync(region, cancellationToken);
    }

    public async Task<RouteGraphSnapshot> LoadSnapshotAsync(string region, CancellationToken cancellationToken)
    {
        var version = await GetCurrentVersionAsync(region, cancellationToken);

        var nodes = await _dbContext.LogisticsNodes
            .AsNoTracking()
            .Where(x => x.Region == region)
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        var nodeIds = nodes.Select(x => x.Id).ToHashSet();

        var lanes = await _dbContext.LogisticsLanes
            .AsNoTracking()
            .Include(x => x.Schedules)
            .Where(x => x.Status == LaneStatus.Active)
            .Where(x => nodeIds.Contains(x.OriginNodeId) && nodeIds.Contains(x.DestinationNodeId))
            .ToListAsync(cancellationToken);

        var coverages = await _dbContext.PostalCoverages
            .AsNoTracking()
            .Where(x => nodeIds.Contains(x.DestinationNodeId))
            .ToListAsync(cancellationToken);

        var graphNodes = nodes.ToDictionary(
            x => x.Id,
            x => new GraphNode(x.Id, x.Code, x.Region, x.TimeZoneId, x.HandlingMinutes));

        var edges = lanes.Select(x => new GraphEdge(
            x.Id,
            x.OriginNodeId,
            x.DestinationNodeId,
            x.CarrierCode,
            x.Mode,
            x.TransitMinutes,
            x.MaximumWeightKg,
            x.MaximumCubicWeightKg,
            x.SupportsFragileItems,
            x.SupportsRestrictedItems,
            x.Schedules
                .Where(s => s.IsActive)
                .Select(s => new WeeklyDeparture(s.DayOfWeek, s.DepartureTime))
                .ToList())).ToList();

        var adjacency = edges
            .GroupBy(x => x.OriginNodeId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<GraphEdge>)x.ToList());

        var graphCoverages = coverages
            .Select(x => new CoverageRange(
                x.PostalCodeFrom,
                x.PostalCodeTo,
                x.DestinationNodeId,
                x.Priority))
            .ToList();

        return new RouteGraphSnapshot(
            version,
            DateTimeOffset.UtcNow,
            graphNodes,
            adjacency,
            graphCoverages);
    }

    public async Task<LogisticsNode> CreateNodeAsync(CreateNodeRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var node = new LogisticsNode(
            request.Code,
            request.Name,
            request.Region,
            request.TimeZoneId,
            request.Type,
            request.HandlingMinutes);

        _dbContext.LogisticsNodes.Add(node);

        var networkVersion = await EnsureNetworkVersionAsync(node.Region, cancellationToken);
        networkVersion.Increment();
        AddOutbox("RoutingNetworkChanged", new
        {
            Entity = "LogisticsNode",
            NodeId = node.Id,
            node.Region,
            NetworkVersion = networkVersion.Version
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return node;
    }

    public async Task<LogisticsLane> CreateLaneAsync(CreateLaneRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await EnsureNodeExistsAsync(request.OriginNodeId, cancellationToken);
        await EnsureNodeExistsAsync(request.DestinationNodeId, cancellationToken);

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

        _dbContext.LogisticsLanes.Add(lane);

        var networkVersion = await EnsureNetworkVersionAsync(request.Region, cancellationToken);
        networkVersion.Increment();
        AddOutbox("RoutingNetworkChanged", new
        {
            Entity = "LogisticsLane",
            LaneId = lane.Id,
            request.Region,
            NetworkVersion = networkVersion.Version
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return lane;
    }

    public async Task<LogisticsLane> UpdateLaneAsync(
        Guid laneId,
        UpdateLaneRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var lane = await _dbContext.LogisticsLanes
            .Include(x => x.Schedules)
            .SingleOrDefaultAsync(x => x.Id == laneId, cancellationToken)
            ?? throw new KeyNotFoundException("Lane not found");

        _dbContext.LaneSchedules.RemoveRange(lane.Schedules);
        lane.Update(
            request.TransitMinutes,
            request.MaximumWeightKg,
            request.MaximumCubicWeightKg,
            request.SupportsFragileItems,
            request.SupportsRestrictedItems,
            request.Schedules.Select(ToSchedule));

        var networkVersion = await EnsureNetworkVersionAsync(request.Region, cancellationToken);
        networkVersion.Increment();
        AddOutbox("RoutingNetworkChanged", new
        {
            Entity = "LogisticsLane",
            LaneId = lane.Id,
            request.Region,
            NetworkVersion = networkVersion.Version
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return lane;
    }

    public async Task<LogisticsLane> ChangeLaneStatusAsync(
        Guid laneId,
        ChangeLaneStatusRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var lane = await _dbContext.LogisticsLanes
            .SingleOrDefaultAsync(x => x.Id == laneId, cancellationToken)
            ?? throw new KeyNotFoundException("Lane not found");

        lane.ChangeStatus(request.Status);

        var networkVersion = await EnsureNetworkVersionAsync(request.Region, cancellationToken);
        networkVersion.Increment();
        AddOutbox("RoutingNetworkChanged", new
        {
            Entity = "LogisticsLane",
            LaneId = lane.Id,
            request.Region,
            LaneStatus = lane.Status,
            NetworkVersion = networkVersion.Version
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return lane;
    }

    private async Task<NetworkVersion> EnsureNetworkVersionAsync(string region, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required", nameof(region));

        var trimmedRegion = region.Trim();
        var networkVersion = await _dbContext.NetworkVersions
            .SingleOrDefaultAsync(x => x.Region == trimmedRegion, cancellationToken);

        if (networkVersion is not null)
            return networkVersion;

        networkVersion = new NetworkVersion(trimmedRegion);
        _dbContext.NetworkVersions.Add(networkVersion);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return networkVersion;
    }

    private async Task EnsureNodeExistsAsync(Guid nodeId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.LogisticsNodes
            .AnyAsync(x => x.Id == nodeId, cancellationToken);

        if (!exists)
            throw new KeyNotFoundException($"Node {nodeId} was not found");
    }

    private void AddOutbox(string type, object payload)
    {
        _dbContext.OutboxMessages.Add(new OutboxMessage(
            type,
            JsonSerializer.Serialize(payload)));
    }

    private static LaneSchedule ToSchedule(LaneScheduleDto schedule)
    {
        return new LaneSchedule(schedule.DayOfWeek, schedule.DepartureTime, schedule.IsActive);
    }
}
