namespace FlashStream.Application.Configuration;

public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; init; } = string.Empty;
    public int ProductCacheDurationSeconds { get; init; }
}
