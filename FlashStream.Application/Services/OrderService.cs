using System.Text.Json;
using FlashStream.Application.Configuration;
using FlashStream.Application.Contracts;
using FlashStream.Domain.Entities;
using FlashStream.Domain.Events;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FlashStream.Application.Services;

public sealed class OrderService(
    IOrderRepository orders,
    IProductRepository products,
    IOrderRateLimiter rateLimiter,
    IValidator<CreateOrderCommand> validator,
    IOptions<KafkaSettings> kafkaSettings) : IOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    public async Task<CreateOrderResult> CreateAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var rateLimit = await rateLimiter.CheckAsync(command.UserId, cancellationToken);

        if (!rateLimit.Allowed)
        {
            throw new OrderRateLimitExceededException(rateLimit.RetryAfterSeconds);
        }

        var idempotencyKey = Guid.Parse(command.IdempotencyKey).ToString("N");
        var existingOrder = await orders.GetByIdempotencyKeyAsync(
            command.UserId,
            idempotencyKey,
            cancellationToken);
        if (existingOrder is not null)
        {
            EnsureSameRequest(existingOrder, command);
            return new CreateOrderResult(ToResponse(existingOrder), false);
        }

        var product = await products.GetByIdAsync(command.ProductId, cancellationToken)
            ?? throw new ProductNotFoundException(command.ProductId);
        var now = DateTimeOffset.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            ProductId = product.Id,
            Quantity = command.Quantity,
            UnitPrice = product.Price,
            IdempotencyKey = idempotencyKey,
            Status = OrderStatuses.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        var orderEvent = new OrderRequestedEvent(
            Guid.NewGuid(),
            order.Id,
            order.UserId,
            order.ProductId,
            order.Quantity,
            now);
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = kafkaSettings.Value.OrderRequestsTopic,
            Key = order.Id.ToString(),
            Payload = JsonSerializer.Serialize(orderEvent, JsonOptions),
            CreatedAt = now
        };

        var persistedOrder = await orders.CreateAsync(order, outboxMessage, cancellationToken);
        EnsureSameRequest(persistedOrder, command);
        return new CreateOrderResult(ToResponse(persistedOrder), persistedOrder.Id == order.Id);
    }

    public async Task<OrderResponse?> GetAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(orderId, cancellationToken);
        return order is null ? null : ToResponse(order);
    }

    private static void EnsureSameRequest(Order order, CreateOrderCommand command)
    {
        if (order.ProductId != command.ProductId || order.Quantity != command.Quantity)
        {
            throw new IdempotencyConflictException();
        }
    }

    private static OrderResponse ToResponse(Order order) =>
        new(order.Id, order.ProductId, order.Quantity, order.UnitPrice, order.Status, order.FailureReason);
}
