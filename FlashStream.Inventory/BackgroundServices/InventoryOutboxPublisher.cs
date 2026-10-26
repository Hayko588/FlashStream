using Confluent.Kafka;
using FlashStream.Inventory.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Inventory.BackgroundServices;

public sealed class InventoryOutboxPublisher(
    IServiceScopeFactory scopes,
    IProducer<string, string> producer,
    IConfiguration configuration,
    ILogger<InventoryOutboxPublisher> logger) : BackgroundService
{
    private TimeSpan RetryDelay => TimeSpan.FromSeconds(configuration.GetValue<int>("Outbox:RetryDelaySeconds"));
    private TimeSpan LeaseDuration => TimeSpan.FromSeconds(configuration.GetValue<int>("Outbox:LeaseSeconds"));
    private TimeSpan PollInterval => TimeSpan.FromMilliseconds(configuration.GetValue<int>("Outbox:PollIntervalMilliseconds"));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            OutboxMessage? message;
            try
            {
                message = await ClaimAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Could not claim an Inventory outbox message.");
                await Task.Delay(RetryDelay, stoppingToken);
                continue;
            }

            if (message is null)
            {
                await Task.Delay(PollInterval, stoppingToken);
                continue;
            }

            try
            {
                await producer.ProduceAsync(
                    message.Topic,
                    new Message<string, string> { Key = message.Key, Value = message.Payload },
                    stoppingToken);
                await MarkPublishedAsync(message.Id, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Could not publish Inventory outbox message {MessageId}.", message.Id);
                await ReleaseAsync(message.Id, stoppingToken);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task<OutboxMessage?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var message = await database.OutboxMessages
            .FromSqlRaw("""
                SELECT * FROM "OutboxMessages"
                WHERE "PublishedAt" IS NULL
                  AND ("LockedUntil" IS NULL OR "LockedUntil" < now())
                ORDER BY "CreatedAt"
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .FirstOrDefaultAsync(cancellationToken);
        if (message is null)
        {
            return null;
        }

        message.LockedUntil = DateTimeOffset.UtcNow.Add(LeaseDuration);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return message;
    }

    private async Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var message = await database.OutboxMessages.SingleAsync(item => item.Id == id, cancellationToken);
        message.PublishedAt = DateTimeOffset.UtcNow;
        message.LockedUntil = null;
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var message = await database.OutboxMessages.SingleAsync(item => item.Id == id, cancellationToken);
        message.LockedUntil = DateTimeOffset.UtcNow.Add(RetryDelay);
        await database.SaveChangesAsync(cancellationToken);
    }
}
