using System.Text.Json;
using Confluent.Kafka;
using FlashStream.Contracts;
using FlashStream.Inventory.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Inventory.BackgroundServices;

public sealed class ReservationRequestConsumer(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<ReservationRequestConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrap = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");
        var requestTopic = configuration["Kafka:InventoryReservationRequestsTopic"]
            ?? throw new InvalidOperationException("Kafka request topic is required.");
        var resultTopic = configuration["Kafka:InventoryReservationResultsTopic"]
            ?? throw new InvalidOperationException("Kafka result topic is required.");
        var consumerGroup = configuration["Kafka:InventoryConsumerGroup"]
            ?? throw new InvalidOperationException("Kafka inventory consumer group is required.");

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = consumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        }).Build();
        consumer.Subscribe(requestTopic);
        logger.LogInformation("Inventory consumes reservation requests from {Topic}", requestTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await Task.Run(() => consumer.Consume(stoppingToken), stoppingToken);
            var request = JsonSerializer.Deserialize<InventoryReservationRequested>(
                result.Message.Value,
                JsonSerializerOptions.Web)
                ?? throw new InvalidDataException("Received an empty inventory reservation request.");
            await ProcessAsync(request, stoppingToken);
            consumer.Commit(result);
        }
    }

    private async Task ProcessAsync(
        InventoryReservationRequested request,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        if (await database.ProcessedReservations.AnyAsync(
                item => item.EventId == request.EventId,
                cancellationToken))
        {
            return;
        }

        var inventory = await database.Inventory
            .FromSqlInterpolated(
                $"SELECT * FROM \"Inventory\" WHERE \"ProductId\" = {request.ProductId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        var reserved = inventory is not null && request.Quantity > 0 &&
                       inventory.AvailableQuantity >= request.Quantity;
        var rejectionReason = reserved ? null : "Insufficient inventory.";
        if (reserved)
        {
            inventory!.AvailableQuantity -= request.Quantity;
            inventory.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var now = DateTimeOffset.UtcNow;
        var remainingInventory = inventory?.AvailableQuantity ?? 0;
        database.ProcessedReservations.Add(new ProcessedReservation
        {
            EventId = request.EventId,
            OrderId = request.OrderId,
            Reserved = reserved,
            RemainingInventory = remainingInventory,
            RejectionReason = rejectionReason,
            ProcessedAt = now
        });
        var completed = new InventoryReservationCompleted(
            Guid.NewGuid(),
            request.OrderId,
            request.ProductId,
            request.Quantity,
            reserved,
            remainingInventory,
            rejectionReason,
            now);
        database.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = configuration["Kafka:InventoryReservationResultsTopic"]
                ?? throw new InvalidOperationException("Kafka result topic is required."),
            Key = request.OrderId.ToString(),
            Payload = JsonSerializer.Serialize(completed, JsonSerializerOptions.Web),
            CreatedAt = now
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
