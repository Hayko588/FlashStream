using FlashStream.Domain.Entities;
using FlashStream.Domain.Events;

namespace FlashStream.Application.Contracts;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken);
}

public interface IOrderRepository
{
    Task<Order?> GetByIdempotencyKeyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Order> CreateAsync(
        Order order,
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken);
}

public interface IOrderRateLimiter
{
    Task<RateLimitResult> CheckAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IProductCache
{
    Task<ProductResponse?> GetAsync(Guid productId, CancellationToken cancellationToken);
    Task SetAsync(Product product, CancellationToken cancellationToken);
    Task InvalidateAsync(Guid productId, CancellationToken cancellationToken);
}

public interface IApplicationHealth
{
    Task<bool> IsDatabaseAvailableAsync(CancellationToken cancellationToken);
}

public interface IProductService
{
    Task<ProductResponse?> GetAsync(Guid productId, CancellationToken cancellationToken);
}

public interface IOrderService
{
    Task<CreateOrderResult> CreateAsync(CreateOrderCommand command, CancellationToken cancellationToken);
    Task<OrderResponse?> GetAsync(Guid orderId, CancellationToken cancellationToken);
}

public interface IHealthService
{
    Task<HealthResult> CheckAsync(CancellationToken cancellationToken);
}
