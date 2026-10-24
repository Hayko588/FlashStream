using System.Text.Json;
using FlashStream.Application.Configuration;
using FlashStream.Application.Contracts;
using FlashStream.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FlashStream.Infrastructure;

public sealed class ProductCache(
    IConnectionMultiplexer redis,
    IOptions<RedisSettings> settings,
    ILogger<ProductCache> logger) : IProductCache
{
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly TimeSpan _cacheDuration =
        TimeSpan.FromSeconds(settings.Value.ProductCacheDurationSeconds);

    public async Task<ProductResponse?> GetAsync(Guid productId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var value = await _database.StringGetAsync(CacheKey(productId));
            return value.IsNull
                ? null
                : JsonSerializer.Deserialize<ProductResponse>(value.ToString(), JsonSerializerOptions.Web);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Redis cache read failed for product {ProductId}", productId);
            return null;
        }
    }

    public async Task SetAsync(Product product, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = new ProductResponse(product.Id, product.Name, product.Price, product.Inventory);
        var json = JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
        try
        {
            await _database.StringSetAsync(CacheKey(product.Id), json, _cacheDuration);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Redis cache write failed for product {ProductId}", product.Id);
        }
    }

    public async Task InvalidateAsync(Guid productId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await _database.KeyDeleteAsync(CacheKey(productId));
        }
        catch (RedisException exception)
        {
            logger.LogWarning(
                exception,
                "Redis cache invalidation failed for product {ProductId}; the cache will expire by TTL",
                productId);
        }
    }

    private static RedisKey CacheKey(Guid productId) => $"products:{productId}";
}
