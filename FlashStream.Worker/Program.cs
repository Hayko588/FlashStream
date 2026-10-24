using FlashStream.Infrastructure;
using FlashStream.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddFlashStreamInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OrderEventConsumer>();

var host = builder.Build();
await host.RunAsync();
