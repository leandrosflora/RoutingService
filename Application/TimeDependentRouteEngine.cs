using RoutingService.Contracts;
using RoutingService.Graph;

namespace RoutingService.Application;

public sealed class TimeDependentRouteEngine
{
    private const int MaximumLegs = 6;

    public IReadOnlyList<CalculatedRoute> Search(
        RouteGraphSnapshot graph,
        Guid originNodeId,
        IReadOnlySet<Guid> destinationNodeIds,
        PackageProfileDto package,
        DateTimeOffset requestedAtUtc,
        int maxOptions)
    {
        if (!graph.Nodes.ContainsKey(originNodeId))
            throw new ArgumentException("Origin node does not exist", nameof(originNodeId));

        var bestArrival = new Dictionary<Guid, DateTimeOffset> { [originNodeId] = requestedAtUtc };
        var legCounts = new Dictionary<Guid, int> { [originNodeId] = 0 };
        var previous = new Dictionary<Guid, PreviousStep>();
        var settled = new HashSet<Guid>();
        var queue = new PriorityQueue<Guid, long>();
        var routes = new List<CalculatedRoute>();

        queue.Enqueue(originNodeId, requestedAtUtc.UtcTicks);

        while (queue.TryDequeue(out var currentNodeId, out _))
        {
            if (!settled.Add(currentNodeId))
                continue;

            if (destinationNodeIds.Contains(currentNodeId) && currentNodeId != originNodeId)
            {
                routes.Add(Reconstruct(graph, originNodeId, currentNodeId, requestedAtUtc, previous));

                if (routes.Count >= maxOptions)
                    break;
            }

            if (!graph.Adjacency.TryGetValue(currentNodeId, out var outgoingEdges))
                continue;

            var currentArrival = bestArrival[currentNodeId];
            var currentLegCount = legCounts[currentNodeId];

            if (currentLegCount >= MaximumLegs)
                continue;

            var originNode = graph.Nodes[currentNodeId];

            foreach (var edge in outgoingEdges)
            {
                if (!Supports(edge, package))
                    continue;

                if (!graph.Nodes.TryGetValue(edge.DestinationNodeId, out var destinationNode))
                    continue;

                var departureAt = FindNextDepartureUtc(edge.Departures, originNode.TimeZoneId, currentArrival);

                if (departureAt is null)
                    continue;

                var arrivalAt = departureAt.Value
                    .AddMinutes(edge.TransitMinutes)
                    .AddMinutes(destinationNode.HandlingMinutes);

                var hasBetterRoute =
                    !bestArrival.TryGetValue(destinationNode.Id, out var knownArrival)
                    || arrivalAt < knownArrival;

                if (!hasBetterRoute)
                    continue;

                bestArrival[destinationNode.Id] = arrivalAt;
                legCounts[destinationNode.Id] = currentLegCount + 1;
                previous[destinationNode.Id] = new PreviousStep(
                    currentNodeId,
                    edge,
                    departureAt.Value,
                    arrivalAt);

                queue.Enqueue(destinationNode.Id, arrivalAt.UtcTicks);
            }
        }

        return routes.OrderBy(x => x.ArrivalAt).ToList();
    }

    private static bool Supports(GraphEdge edge, PackageProfileDto package)
    {
        if (package.WeightKg > edge.MaximumWeightKg)
            return false;

        if (package.CubicWeightKg > edge.MaximumCubicWeightKg)
            return false;

        if (package.IsFragile && !edge.SupportsFragileItems)
            return false;

        if (package.IsRestricted && !edge.SupportsRestrictedItems)
            return false;

        return true;
    }

    private static DateTimeOffset? FindNextDepartureUtc(
        IReadOnlyList<WeeklyDeparture> schedules,
        string timeZoneId,
        DateTimeOffset availableAtUtc)
    {
        if (schedules.Count == 0)
            return availableAtUtc;

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localAvailable = TimeZoneInfo.ConvertTime(availableAtUtc, timeZone);

        for (var offset = 0; offset <= 7; offset++)
        {
            var date = DateOnly.FromDateTime(localAvailable.DateTime).AddDays(offset);
            var departuresForDay = schedules
                .Where(x => x.DayOfWeek == date.DayOfWeek)
                .OrderBy(x => x.DepartureTime);

            foreach (var schedule in departuresForDay)
            {
                var localDateTime = date.ToDateTime(schedule.DepartureTime, DateTimeKind.Unspecified);

                if (offset == 0 && localDateTime < localAvailable.DateTime)
                    continue;

                if (timeZone.IsInvalidTime(localDateTime))
                    continue;

                var utcOffset = timeZone.GetUtcOffset(localDateTime);

                return new DateTimeOffset(localDateTime, utcOffset).ToUniversalTime();
            }
        }

        return null;
    }

    private static CalculatedRoute Reconstruct(
        RouteGraphSnapshot graph,
        Guid originNodeId,
        Guid destinationNodeId,
        DateTimeOffset requestedAt,
        IReadOnlyDictionary<Guid, PreviousStep> previous)
    {
        var legs = new List<CalculatedRouteLeg>();
        var currentNodeId = destinationNodeId;

        while (currentNodeId != originNodeId)
        {
            if (!previous.TryGetValue(currentNodeId, out var step))
                throw new InvalidOperationException("Could not reconstruct route");

            var origin = graph.Nodes[step.PreviousNodeId];
            var destination = graph.Nodes[currentNodeId];

            legs.Add(new CalculatedRouteLeg(step.Edge, origin, destination, step.DepartureAt, step.ArrivalAt));
            currentNodeId = step.PreviousNodeId;
        }

        legs.Reverse();

        return new CalculatedRoute(
            destinationNodeId,
            requestedAt,
            legs[0].DepartureAt,
            legs[^1].ArrivalAt,
            legs);
    }

    private sealed record PreviousStep(
        Guid PreviousNodeId,
        GraphEdge Edge,
        DateTimeOffset DepartureAt,
        DateTimeOffset ArrivalAt
    );
}
