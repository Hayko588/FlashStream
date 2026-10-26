using System.Text.Json;
using Confluent.Kafka;
using FlashStream.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Ordering.BackgroundServices;

public sealed class InventoryResultConsumer(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<InventoryResultConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrap = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");
        var topic = configuration["Kafka:InventoryReservationResultsTopic"]
            ?? throw new InvalidOperationException("Kafka result topic is required.");
        var group = configuration["Kafka:OrderingConsumerGroup"]
            ?? throw new InvalidOperationException("Kafka ordering consumer group is required.");
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = group,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        }).Build();
        consumer.Subscribe(topic);
        logger.LogInformation("Ordering consumes inventory results from {Topic}", topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await Task.Run(() => consumer.Consume(stoppingToken), stoppingToken);
            var completed = JsonSerializer.Deserialize<InventoryReservationCompleted>(
                result.Message.Value,
                JsonSerializerOptions.Web)
                ?? throw new InvalidDataException("Received an empty inventory reservation result.");
            await ApplyResultAsync(completed, stoppingToken);
            consumer.Commit(result);
        }
    }

    private async Task ApplyResultAsync(
        InventoryReservationCompleted completed,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await database.ProcessedEvents.AnyAsync(
                item => item.EventId == completed.EventId,
                cancellationToken))
        {
            return;
        }

        var order = await database.Orders.SingleOrDefaultAsync(
            item => item.Id == completed.OrderId,
            cancellationToken)
            ?? throw new InvalidDataException($"Order {completed.OrderId} does not exist.");
        if (order.ProductId != completed.ProductId || order.Quantity != completed.Quantity ||
            order.Status != "PendingInventory")
        {
            throw new InvalidDataException($"Inventory result does not match pending order {order.Id}.");
        }

        order.Status = completed.Reserved ? "Confirmed" : "Rejected";
        order.FailureReason = completed.RejectionReason;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        database.ProcessedEvents.Add(new FlashStream.Ordering.Entities.ProcessedEvent
        {
            EventId = completed.EventId,
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Order {OrderId} transitioned to {Status}", order.Id, order.Status);
    }
}
