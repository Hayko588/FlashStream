using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FlashStream.Application.Configuration;
using FlashStream.Infrastructure.Configuration;
using FlashStream.Infrastructure;
using FlashStream.Domain.Entities;
using FlashStream.Domain.Events;
using Microsoft.EntityFrameworkCore;
using FlashStream.Application.Contracts;
using Microsoft.Extensions.Options;

namespace FlashStream.Worker;

public sealed class OrderEventConsumer(
    IServiceScopeFactory scopeFactory,
    IProductCache productCache,
    IOptions<KafkaSettings> kafkaSettings,
    ILogger<OrderEventConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = kafkaSettings.Value;
        var topic = settings.OrderRequestsTopic;
        var updatesTopic = settings.OrderUpdatesTopic;
        using (var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = settings.BootstrapServers
        }).Build())
        {
            try
            {
                await adminClient.CreateTopicsAsync(
                [
                    new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = settings.TopicPartitions,
                        ReplicationFactor = settings.ReplicationFactor
                    },
                    new TopicSpecification
                    {
                        Name = updatesTopic,
                        NumPartitions = settings.TopicPartitions,
                        ReplicationFactor = settings.ReplicationFactor
                    }
                ]);
            }
            catch (CreateTopicsException exception) when (
                exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                logger.LogDebug("Kafka order topics already exist.");
            }
        }

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(topic);
        logger.LogInformation("Consuming order events from {Topic}", topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await Task.Run(() => consumer.Consume(stoppingToken), stoppingToken);
            var requestedEvent = JsonSerializer.Deserialize<OrderRequestedEvent>(result.Message.Value, JsonOptions)
                ?? throw new InvalidDataException("Received an empty order event.");

            await ProcessAsync(requestedEvent, stoppingToken);
            consumer.Commit(result);
        }
    }

    private async Task ProcessAsync(OrderRequestedEvent requestedEvent, CancellationToken cancellationToken)
    {
        Guid productId;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlashStreamDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            var alreadyProcessed = await database.ProcessedEvents
                .AnyAsync(item => item.EventId == requestedEvent.EventId, cancellationToken);
            if (alreadyProcessed)
            {
                productId = requestedEvent.ProductId;
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                var product = await database.Products
                    .FromSqlInterpolated(
                        $"SELECT * FROM \"Products\" WHERE \"Id\" = {requestedEvent.ProductId} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidDataException($"Product {requestedEvent.ProductId} does not exist.");
                var order = await database.Orders.SingleOrDefaultAsync(
                    item => item.Id == requestedEvent.OrderId,
                    cancellationToken)
                    ?? throw new InvalidDataException($"Order {requestedEvent.OrderId} does not exist.");

                if (order.Status != OrderStatuses.Pending ||
                    order.UserId != requestedEvent.UserId ||
                    order.ProductId != requestedEvent.ProductId ||
                    order.Quantity != requestedEvent.Quantity)
                {
                    throw new InvalidDataException($"Order event {requestedEvent.EventId} does not match its order.");
                }

                string? failureReason = null;
                if (product.Inventory >= order.Quantity)
                {
                    product.Inventory -= order.Quantity;
                    product.UpdatedAt = DateTimeOffset.UtcNow;
                    order.Status = OrderStatuses.Confirmed;
                }
                else
                {
                    order.Status = OrderStatuses.Rejected;
                    failureReason = "Insufficient inventory.";
                    order.FailureReason = failureReason;
                }

                var now = DateTimeOffset.UtcNow;
                order.UpdatedAt = now;
                database.ProcessedEvents.Add(new ProcessedEvent
                {
                    EventId = requestedEvent.EventId,
                    ProcessedAt = now
                });

                var stateChangedEvent = new OrderStateChangedEvent(
                    Guid.NewGuid(),
                    order.Id,
                    order.Status,
                    failureReason,
                    product.Inventory,
                    now);
                database.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Topic = kafkaSettings.Value.OrderUpdatesTopic,
                    Key = order.Id.ToString(),
                    Payload = JsonSerializer.Serialize(stateChangedEvent, JsonOptions),
                    CreatedAt = now
                });

                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                productId = product.Id;
                logger.LogInformation("Order {OrderId} transitioned to {OrderStatus}", order.Id, order.Status);
            }
        }

        await productCache.InvalidateAsync(productId, cancellationToken);
    }
}
