namespace FlashStream.Application.Contracts;

public sealed class ProductNotFoundException(Guid productId)
    : Exception($"Product '{productId}' was not found.");

public sealed class OrderNotFoundException(Guid orderId)
    : Exception($"Order '{orderId}' was not found.");

public sealed class IdempotencyConflictException()
    : Exception("The idempotency key was already used for a different order.");

public sealed class OrderRateLimitExceededException(int retryAfterSeconds)
    : Exception("Order request rate limit exceeded.")
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}

public sealed class RateLimiterUnavailableException(Exception innerException)
    : Exception("The order rate limiter is unavailable.", innerException);
