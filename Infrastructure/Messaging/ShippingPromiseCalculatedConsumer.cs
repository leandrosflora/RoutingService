using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using RoutingService.Application.Ports;
using RoutingService.Contracts.Events;

namespace RoutingService.Infrastructure.Messaging;

public sealed class ShippingPromiseCalculatedConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<ShippingPromiseCalculatedConsumer> logger) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.UseMock)
        {
            logger.LogInformation("Kafka consumer disabled because Kafka:UseMock=true");
            return;
        }

        await Task.Yield();
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();

        consumer.Subscribe(_options.Topics.ShippingPromiseCalculated);
        logger.LogInformation("Kafka consumer subscribed topic={Topic} groupId={GroupId}", _options.Topics.ShippingPromiseCalculated, _options.ConsumerGroupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var envelope = JsonSerializer.Deserialize<EventEnvelope<ShippingPromiseCalculatedPayload>>(result.Message.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (envelope is null)
                {
                    logger.LogWarning("Kafka message ignored topic={Topic} key={MessageKey}: empty envelope", result.Topic, result.Message.Key);
                    consumer.Commit(result);
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IShippingPromiseStore>();
                await store.TryRegisterAsync(envelope.EventId, envelope.CorrelationId, envelope.Payload.CheckoutId, envelope.Payload, stoppingToken);

                logger.LogInformation(
                    "Consumed Kafka event topic={Topic} key={MessageKey} eventType={EventType} correlationId={CorrelationId}",
                    result.Topic,
                    result.Message.Key,
                    envelope.EventType,
                    envelope.CorrelationId);

                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Kafka consumer processing failed topic={Topic}", _options.Topics.ShippingPromiseCalculated);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
