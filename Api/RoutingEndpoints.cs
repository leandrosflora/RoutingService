using RoutingService.Application;
using RoutingService.Contracts;

namespace RoutingService.Api;

public static class RoutingEndpoints
{
    public static IEndpointRouteBuilder MapRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        var routes = app.MapGroup("/v1/routes").WithTags("Routes");
        routes.MapPost("/calculate", CalculateRoutesAsync);
        routes.MapGet("/{routeId}", GetRouteAsync);

        return app;
    }

    private static async Task<IResult> CalculateRoutesAsync(
        SearchRoutesRequest request,
        HttpContext httpContext,
        RouteSearchService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = ResolveHeader(httpContext, "X-Correlation-Id") ?? httpContext.TraceIdentifier;
            var response = await service.SearchAsync(request, correlationId, cancellationToken);
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

    private static async Task<IResult> GetRouteAsync(
        string routeId,
        RouteSearchService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var route = await service.GetRouteAsync(routeId, cancellationToken);
            return route is null
                ? Results.NotFound(new { error = "Route not found" })
                : Results.Ok(route);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static string? ResolveHeader(HttpContext httpContext, string name)
    {
        return httpContext.Request.Headers.TryGetValue(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
