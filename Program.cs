using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RoutingService.Api;
using RoutingService.Application;
using RoutingService.Application.Ports;
using RoutingService.Graph;
using RoutingService.Infrastructure.Cache;
using RoutingService.Infrastructure.Persistence;
using RoutingService.Infrastructure.Workers;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Environment.ApplicationName;
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:5107";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var useMockRepository = builder.Configuration.GetValue<bool>("Routing:UseMockRepository");

if (!useMockRepository)
{
    builder.Services.AddDbContext<RoutingDbContext>(options =>
    {
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("RoutingDb")
            ?? "Host=localhost;Port=5432;Database=logistica_envios;Username=logistica;Password=logistica;Search Path=routing,public");
    });
}

if (useMockRepository)
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration =
            builder.Configuration.GetConnectionString("Redis")
            ?? "localhost:6379";

        options.InstanceName = "routing:";
    });
}

builder.Services.AddScoped<RouteSearchService>();
builder.Services.AddScoped<RouteGraphLoader>();
if (useMockRepository)
{
    builder.Services.AddScoped<IRoutingNetworkRepository, MockRoutingNetworkRepository>();
}
else
{
    builder.Services.AddScoped<IRoutingNetworkRepository, RoutingNetworkRepository>();
}
builder.Services.AddScoped<IRouteSearchCache, RedisRouteSearchCache>();
builder.Services.AddSingleton<ICalculatedRouteStore, InMemoryCalculatedRouteStore>();

builder.Services.AddSingleton<TimeDependentRouteEngine>();
builder.Services.AddSingleton<RouteGraphStore>();

builder.Services.AddHostedService<RouteGraphRefreshWorker>();

var healthChecks = builder.Services.AddHealthChecks();
if (!useMockRepository)
{
    healthChecks.AddDbContextCheck<RoutingDbContext>();
}

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.MapHealthChecks("/health");
app.MapRoutingEndpoints();

app.Run();
