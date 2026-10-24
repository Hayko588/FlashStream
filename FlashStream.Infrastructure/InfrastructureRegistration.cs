using Confluent.Kafka;
using FlashStream.Application.Configuration;
using FlashStream.Application.Contracts;
using FlashStream.Infrastructure.Configuration;
using FlashStream.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace FlashStream.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddFlashStreamInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var postgresConnection = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
        services.AddDbContext<FlashStreamDbContext>(options => options.UseNpgsql(postgresConnection));
        services.AddOptions<KafkaSettings>()
            .Bind(configuration.GetSection(KafkaSettings.SectionName))
            .Validate(settings =>
                !string.IsNullOrWhiteSpace(settings.BootstrapServers) &&
                !string.IsNullOrWhiteSpace(settings.OrderRequestsTopic) &&
                !string.IsNullOrWhiteSpace(settings.OrderUpdatesTopic) &&
                !string.IsNullOrWhiteSpace(settings.ConsumerGroup) &&
                settings.TopicPartitions > 0 &&
                settings.ReplicationFactor > 0,
                "Kafka configuration must specify bootstrap servers, topics, group, and positive partition count.")
            .ValidateOnStart();
        services.AddOptions<RedisSettings>()
            .Bind(configuration.GetSection(RedisSettings.SectionName))
            .Validate(settings =>
                !string.IsNullOrWhiteSpace(settings.ConnectionString) &&
                settings.ProductCacheDurationSeconds > 0,
                "Redis connection string and positive product cache duration are required.")
            .ValidateOnStart();
        services.AddOptions<RateLimitSettings>()
            .Bind(configuration.GetSection(RateLimitSettings.SectionName))
            .Validate(settings => settings.RequestsPerWindow > 0 && settings.WindowSeconds > 0,
                "Rate limit count and window must be positive.")
            .ValidateOnStart();
        services.AddOptions<InfrastructureSettings>()
            .Bind(configuration.GetSection(InfrastructureSettings.SectionName))
            .Validate(settings =>
                settings.OutboxRetryDelaySeconds > 0 &&
                settings.OutboxLeaseSeconds > 0 &&
                settings.OutboxPollIntervalMilliseconds > 0,
                "Outbox retry delay and lease duration must be positive.")
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
            ConnectionMultiplexer.Connect(
                serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisSettings>>()
                    .Value.ConnectionString));
        services.AddSingleton<IProducer<string, string>>(serviceProvider =>
            new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = serviceProvider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<KafkaSettings>>()
                    .Value.BootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true
            }).Build());
        services.AddSingleton<ProductCache>();
        services.AddSingleton<IProductCache>(serviceProvider => serviceProvider.GetRequiredService<ProductCache>());
        services.AddScoped<IOrderRateLimiter, OrderRateLimiter>();
        services.AddScoped<FlashStreamRepository>();
        services.AddScoped<IProductRepository>(serviceProvider =>
            serviceProvider.GetRequiredService<FlashStreamRepository>());
        services.AddScoped<IOrderRepository>(serviceProvider =>
            serviceProvider.GetRequiredService<FlashStreamRepository>());
        services.AddScoped<IApplicationHealth>(serviceProvider =>
            serviceProvider.GetRequiredService<FlashStreamRepository>());
        services.AddHostedService<OutboxPublisher>();

        return services;
    }
}
