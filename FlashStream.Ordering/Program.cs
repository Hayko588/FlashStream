using Confluent.Kafka;
using FlashStream.Ordering;
using FlashStream.Ordering.BackgroundServices;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Ordering")
    ?? throw new InvalidOperationException("ConnectionStrings:Ordering is required.");
var catalogBaseUrl = builder.Configuration["Services:CatalogBaseUrl"]
    ?? throw new InvalidOperationException("Services:CatalogBaseUrl is required.");
var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
    ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");
if (string.IsNullOrWhiteSpace(builder.Configuration["Kafka:InventoryReservationRequestsTopic"]) ||
    string.IsNullOrWhiteSpace(builder.Configuration["Kafka:InventoryReservationResultsTopic"]) ||
    string.IsNullOrWhiteSpace(builder.Configuration["Kafka:OrderingConsumerGroup"]))
{
    throw new InvalidOperationException("Ordering Kafka topics and consumer group are required.");
}
var maximumQuantity = builder.Configuration.GetValue<int?>("Order:MaximumQuantity")
    ?? throw new InvalidOperationException("Order:MaximumQuantity is required.");
if (maximumQuantity < 1)
{
    throw new InvalidOperationException("Order:MaximumQuantity must be positive.");
}

builder.Services.AddDbContext<OrderingDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddHttpClient("catalog", client => client.BaseAddress = new Uri(catalogBaseUrl));
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = kafkaBootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true
    }).Build());
builder.Services.AddHostedService<OrderOutboxPublisher>();
builder.Services.AddHostedService<InventoryResultConsumer>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
    await database.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.MapControllers();
app.Run();
