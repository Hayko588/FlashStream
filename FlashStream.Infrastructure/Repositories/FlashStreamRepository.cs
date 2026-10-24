using FlashStream.Application.Contracts;
using FlashStream.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FlashStream.Infrastructure.Repositories;

public sealed class FlashStreamRepository(FlashStreamDbContext database)
    : IProductRepository, IOrderRepository, IApplicationHealth
{
    public Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken) =>
        database.Products.AsNoTracking()
            .SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);

    public Task<Order?> GetByIdempotencyKeyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        database.Orders.AsNoTracking()
            .SingleOrDefaultAsync(
                order => order.UserId == userId && order.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<Order?> GetByIdForUserAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken) =>
        database.Orders.AsNoTracking()
            .SingleOrDefaultAsync(
                order => order.Id == orderId && order.UserId == userId,
                cancellationToken);

    public async Task<Order> CreateAsync(
        Order order,
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        database.Orders.Add(order);
        database.OutboxMessages.Add(outboxMessage);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return order;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            database.ChangeTracker.Clear();
            var existingOrder = await GetByIdempotencyKeyAsync(
                order.UserId,
                order.IdempotencyKey,
                cancellationToken);
            if (existingOrder is null)
            {
                throw;
            }

            return existingOrder;
        }
    }

    public Task<bool> IsDatabaseAvailableAsync(CancellationToken cancellationToken) =>
        database.Database.CanConnectAsync(cancellationToken);
}
