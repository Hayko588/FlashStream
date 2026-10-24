namespace FlashStream.Application.Configuration;

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimit";

    public int RequestsPerWindow { get; init; }
    public int WindowSeconds { get; init; }
}
