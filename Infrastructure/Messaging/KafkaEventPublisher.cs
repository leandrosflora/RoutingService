using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using RoutingService.Application.Ports;
using RoutingService.Contracts.Events;

namespace RoutingService.Infrastructure.Messaging;

public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 5000
        }).Build();
    }

    public async Task PublishAsync<TPayload>(EventEnvelope<TPayload> envelope, string messageKey, CancellationToken cancellationToken)
    {
        var topic = envelope.EventType switch
        {
            "checkout.shipping.quote.requested" => _options.Topics.ShippingQuoteRequested,
            _ => envelope.EventType
        };

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        try
        {
            await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = messageKey,
                Value = json,
                Headers =
                [
                    new Header("correlationId", System.Text.Encoding.UTF8.GetBytes(envelope.CorrelationId)),
                    new Header("eventType", System.Text.Encoding.UTF8.GetBytes(envelope.EventType))
                ]
            }, cancellationToken);

            _logger.LogInformation(
                "Published Kafka event topic={Topic} key={MessageKey} eventType={EventType} correlationId={CorrelationId}",
                topic,
                messageKey,
                envelope.EventType,
                envelope.CorrelationId);
        }
        catch (Exception exception) when (exception is ProduceException<string, string> or KafkaException or OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Kafka publish failed topic={Topic} key={MessageKey} eventType={EventType} correlationId={CorrelationId}",
                topic,
                messageKey,
                envelope.EventType,
                envelope.CorrelationId);
        }
    }

    public void Dispose() => _producer.Dispose();
}
