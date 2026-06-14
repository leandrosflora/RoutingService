namespace RoutingService.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ConsumerGroupId { get; set; } = "checkout-service";
    public bool UseMock { get; set; } = true;
    public KafkaTopicsOptions Topics { get; set; } = new();
}

public sealed class KafkaTopicsOptions
{
    public string ShippingQuoteRequested { get; set; } = "checkout.shipping.quote.requested";
    public string ShippingPromiseCalculated { get; set; } = "shipping.promise.calculated";
}
