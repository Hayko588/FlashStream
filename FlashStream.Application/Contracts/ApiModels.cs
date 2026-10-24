namespace FlashStream.Application.Contracts;

public sealed record ProductResponse(Guid Id, string Name, decimal Price, int Inventory);

public sealed record OrderResponse(
    Guid Id,
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    string Status,
    string? FailureReason);

public sealed record CreateOrderCommand(
    Guid UserId,
    Guid ProductId,
    int Quantity,
    string IdempotencyKey);

public sealed record CreateOrderResult(OrderResponse Order, bool Created);
public sealed record RateLimitResult(bool Allowed, int RetryAfterSeconds);
public sealed record HealthResult(bool DatabaseAvailable);
