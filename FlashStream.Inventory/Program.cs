using Confluent.Kafka;
using FlashStream.Inventory;
using FlashStream.Inventory.BackgroundServices;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Inventory")
    ?? throw new InvalidOperationException("ConnectionStrings:Inventory is required.");
var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
    ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");
if (builder.Configuration.GetValue<int>("Outbox:RetryDelaySeconds") < 1 ||
    builder.Configuration.GetValue<int>("Outbox:LeaseSeconds") < 1 ||
    builder.Configuration.GetValue<int>("Outbox:PollIntervalMilliseconds") < 1)
{
    throw new InvalidOperationException("Inventory Kafka and outbox settings must be positive.");
}
builder.Services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = bootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true
    }).Build());
builder.Services.AddHostedService<ReservationRequestConsumer>();
builder.Services.AddHostedService<InventoryOutboxPublisher>();
builder.Services.AddControllers();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await database.Database.EnsureCreatedAsync();
}

app.MapControllers();
app.Run();
