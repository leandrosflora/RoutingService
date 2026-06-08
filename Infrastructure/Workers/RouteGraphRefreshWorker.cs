using RoutingService.Application.Ports;
using RoutingService.Graph;

namespace RoutingService.Infrastructure.Workers;

public sealed class RouteGraphRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RouteGraphStore _graphStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RouteGraphRefreshWorker> _logger;

    public RouteGraphRefreshWorker(
        IServiceScopeFactory scopeFactory,
        RouteGraphStore graphStore,
        IConfiguration configuration,
        ILogger<RouteGraphRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _graphStore = graphStore;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TryRefreshAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await TryRefreshAsync(stoppingToken);
    }

    private async Task TryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRoutingNetworkRepository>();
            var loader = scope.ServiceProvider.GetRequiredService<RouteGraphLoader>();
            var region = _configuration["Routing:Region"] ?? "Brasil Sudeste";
            var databaseVersion = await repository.GetCurrentVersionAsync(region, cancellationToken);

            if (!_graphStore.IsLoaded || _graphStore.Current.Version != databaseVersion)
            {
                await loader.ReloadAsync(cancellationToken);
                _logger.LogInformation("Routing graph updated to version {Version}", databaseVersion);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not refresh routing graph");
        }
    }
}
