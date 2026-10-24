namespace FlashStream.Application.Configuration;

public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = string.Empty;
    public string OrderRequestsTopic { get; init; } = string.Empty;
    public string OrderUpdatesTopic { get; init; } = string.Empty;
    public string ConsumerGroup { get; init; } = string.Empty;
    public int TopicPartitions { get; init; }
    public short ReplicationFactor { get; init; }
}
