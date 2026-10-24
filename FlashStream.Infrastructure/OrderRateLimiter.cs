using FlashStream.Application.Configuration;
using FlashStream.Application.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FlashStream.Infrastructure;

public sealed class OrderRateLimiter(
    IConnectionMultiplexer redis,
    IOptions<RateLimitSettings> settings,
    ILogger<OrderRateLimiter> logger) : IOrderRateLimiter
{
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return { count, redis.call('PTTL', KEYS[1]) }
        """;

    private readonly IDatabase _database = redis.GetDatabase();
    private readonly int _limit = settings.Value.RequestsPerWindow;
    private readonly TimeSpan _window = TimeSpan.FromSeconds(settings.Value.WindowSeconds);

    public async Task<RateLimitResult> CheckAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = new RedisKey($"order-rate:{userId}");
        var windowMilliseconds = Math.Max(1, (long)_window.TotalMilliseconds);
        try
        {
            var result = (RedisResult[]?)(await _database.ScriptEvaluateAsync(
                IncrementScript,
                [key],
                [windowMilliseconds]))
                ?? throw new InvalidOperationException("Redis returned no rate-limit result.");
            if (result.Length != 2)
            {
                throw new InvalidOperationException("Redis returned an invalid rate-limit result.");
            }

            var count = (long)result[0];
            var remainingMilliseconds = Math.Max(0, (long)result[1]);
            return new RateLimitResult(
                count <= _limit,
                (int)Math.Ceiling(remainingMilliseconds / 1000d));
        }
        catch (RedisException exception)
        {
            logger.LogError(exception, "Redis rate limiter is unavailable for user {UserId}", userId);
            throw new RateLimiterUnavailableException(exception);
        }
    }
}
