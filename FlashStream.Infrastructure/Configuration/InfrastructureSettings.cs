namespace FlashStream.Infrastructure.Configuration;

public sealed class InfrastructureSettings
{
    public const string SectionName = "Infrastructure";

    public int OutboxRetryDelaySeconds { get; init; }
    public int OutboxLeaseSeconds { get; init; }
    public int OutboxPollIntervalMilliseconds { get; init; }
}
