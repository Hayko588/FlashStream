using System.Text.Json;
using Confluent.Kafka;
using FlashStream.Infrastructure.Configuration;
using FlashStream.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FlashStream.Infrastructure;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    Microsoft.Extensions.Options.IOptions<InfrastructureSettings> infrastructureSettings,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private readonly TimeSpan _retryDelay =
        TimeSpan.FromSeconds(infrastructureSettings.Value.OutboxRetryDelaySeconds);
    private readonly TimeSpan _leaseDuration =
        TimeSpan.FromSeconds(infrastructureSettings.Value.OutboxLeaseSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            OutboxMessage? message;
            try
            {
                message = await ClaimNextAsync(stoppingToken);
            }
            catch (NpgsqlException exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to read the outbox; retrying");
                await Task.Delay(_retryDelay, stoppingToken);
                continue;
            }

            if (message is null)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(infrastructureSettings.Value.OutboxPollIntervalMilliseconds),
                    stoppingToken);
                continue;
            }

            try
            {
                await producer.ProduceAsync(message.Topic, new Message<string, string>
                {
                    Key = message.Key,
                    Value = message.Payload
                }, stoppingToken);
                await MarkPublishedAsync(message.Id, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to publish outbox message {OutboxMessageId}", message.Id);
                await MarkFailedAsync(message.Id, exception.Message, stoppingToken);
                await Task.Delay(_retryDelay, stoppingToken);
            }
        }
    }

    private async Task<OutboxMessage?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlashStreamDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var message = await database.OutboxMessages
            .FromSqlRaw("""
                SELECT *
                FROM "OutboxMessages"
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

        message.LockedUntil = DateTimeOffset.UtcNow.Add(_leaseDuration);
        message.Attempts++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return message;
    }

    private async Task MarkPublishedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlashStreamDbContext>();
        var message = await database.OutboxMessages.SingleAsync(item => item.Id == messageId, cancellationToken);
        message.PublishedAt = DateTimeOffset.UtcNow;
        message.LockedUntil = null;
        message.LastError = null;
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlashStreamDbContext>();
        var message = await database.OutboxMessages.SingleAsync(item => item.Id == messageId, cancellationToken);
        message.LockedUntil = DateTimeOffset.UtcNow.Add(_retryDelay);
        message.LastError = error.Length <= 2000 ? error : error[..2000];
        await database.SaveChangesAsync(cancellationToken);
    }
}
