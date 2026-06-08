using RoutingService.Application;
using RoutingService.Application.Ports;
using RoutingService.Contracts;
using RoutingService.Graph;

namespace RoutingService.Api;

public static class RoutingEndpoints
{
    public static IEndpointRouteBuilder MapRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        var routes = app.MapGroup("/routes").WithTags("Routes");
        routes.MapPost("/search", SearchRoutesAsync);

        var network = app.MapGroup("/network").WithTags("Network");
        network.MapPost("/nodes", CreateNodeAsync);
        network.MapPost("/lanes", CreateLaneAsync);
        network.MapPut("/lanes/{laneId:guid}", UpdateLaneAsync);
        network.MapPatch("/lanes/{laneId:guid}/status", ChangeLaneStatusAsync);
        network.MapGet("/version", GetNetworkVersionAsync);
        network.MapPost("/reload", ReloadNetworkAsync);

        return app;
    }

    private static async Task<IResult> SearchRoutesAsync(
        SearchRoutesRequest request,
        RouteSearchService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await service.SearchAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("Routing graph", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                title: "Routing graph unavailable",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> CreateNodeAsync(
        CreateNodeRequest request,
        IRoutingNetworkRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            var node = await repository.CreateNodeAsync(request, cancellationToken);
            return Results.Created($"/network/nodes/{node.Id}", node);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> CreateLaneAsync(
        CreateLaneRequest request,
        IRoutingNetworkRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            var lane = await repository.CreateLaneAsync(request, cancellationToken);
            return Results.Created($"/network/lanes/{lane.Id}", lane);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
    }

    private static async Task<IResult> UpdateLaneAsync(
        Guid laneId,
        UpdateLaneRequest request,
        IRoutingNetworkRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            var lane = await repository.UpdateLaneAsync(laneId, request, cancellationToken);
            return Results.Ok(lane);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
    }

    private static async Task<IResult> ChangeLaneStatusAsync(
        Guid laneId,
        ChangeLaneStatusRequest request,
        IRoutingNetworkRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            var lane = await repository.ChangeLaneStatusAsync(laneId, request, cancellationToken);
            return Results.Ok(lane);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
    }

    private static async Task<IResult> GetNetworkVersionAsync(
        string? region,
        IConfiguration configuration,
        IRoutingNetworkRepository repository,
        RouteGraphStore graphStore,
        CancellationToken cancellationToken)
    {
        var resolvedRegion = region ?? configuration["Routing:Region"] ?? "Brasil Sudeste";
        var networkVersion = await repository.GetNetworkVersionAsync(resolvedRegion, cancellationToken);
        var loadedAt = graphStore.IsLoaded ? graphStore.Current.LoadedAt : (DateTimeOffset?)null;

        return Results.Ok(new NetworkVersionResponse(
            networkVersion.Region,
            networkVersion.Version,
            networkVersion.UpdatedAt,
            graphStore.IsLoaded,
            loadedAt));
    }

    private static async Task<IResult> ReloadNetworkAsync(
        RouteGraphLoader loader,
        CancellationToken cancellationToken)
    {
        var snapshot = await loader.ReloadAsync(cancellationToken);

        return Results.Ok(new
        {
            snapshot.Version,
            snapshot.LoadedAt,
            NodeCount = snapshot.Nodes.Count,
            LaneCount = snapshot.Adjacency.Values.Sum(x => x.Count),
            CoverageCount = snapshot.Coverages.Count
        });
    }
}
