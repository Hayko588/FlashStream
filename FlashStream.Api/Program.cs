using FlashStream.Api.Filters;
using FlashStream.Api.Validation;
using FlashStream.Application;
using FlashStream.Application.Configuration;
using FlashStream.Infrastructure;
using FluentValidation;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var requestLimit = builder.Configuration.GetValue<long?>("Security:MaxRequestBodyBytes")
    ?? throw new InvalidOperationException("Security:MaxRequestBodyBytes is required.");
if (requestLimit <= 0)
{
    throw new InvalidOperationException("Security:MaxRequestBodyBytes must be positive.");
}

builder.Services.AddApplication();
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();
builder.Services.AddFlashStreamInfrastructure(builder.Configuration);
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FlashStream API", Version = "v1" });
});
builder.Services.AddOptions<OrderPolicySettings>()
    .Bind(builder.Configuration.GetSection(OrderPolicySettings.SectionName))
    .Validate(settings => settings.MaximumQuantity > 0,
        "OrderPolicy:MaximumQuantity must be positive.")
    .ValidateOnStart();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = requestLimit);

var app = builder.Build();
await DatabaseInitializer.InitializeAsync(app.Services, app.Lifetime.ApplicationStopping);

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = context.Request.Path.StartsWithSegments("/swagger")
        ? "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'"
        : "default-src 'none'; frame-ancestors 'none'";
    context.Response.Headers["Cache-Control"] = "no-store";
    await next();
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();

app.Run();

public partial class Program;
