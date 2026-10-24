using FlashStream.Api.Filters;
using FlashStream.Api.Options;
using FlashStream.Api.Validation;
using FlashStream.Application;
using FlashStream.Application.Configuration;
using FlashStream.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var authentication = builder.Configuration.GetSection(AuthenticationSettings.SectionName)
    .Get<AuthenticationSettings>()
    ?? throw new InvalidOperationException("Authentication configuration is required.");
if (!Uri.TryCreate(authentication.Authority, UriKind.Absolute, out var authorityUri) ||
    !authorityUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
    string.IsNullOrWhiteSpace(authorityUri.Host) ||
    !string.IsNullOrEmpty(authorityUri.UserInfo) ||
    string.IsNullOrWhiteSpace(authentication.Audience))
{
    throw new InvalidOperationException(
        "Authentication:Authority must be an HTTPS URL and Authentication:Audience must be configured.");
}

var requestLimit = builder.Configuration.GetValue<long?>("Security:MaxRequestBodyBytes")
    ?? throw new InvalidOperationException("Security:MaxRequestBodyBytes is required.");
var jwtClockSkewSeconds = builder.Configuration.GetValue<int?>("Security:JwtClockSkewSeconds")
    ?? throw new InvalidOperationException("Security:JwtClockSkewSeconds is required.");
if (requestLimit <= 0 || jwtClockSkewSeconds < 0)
{
    throw new InvalidOperationException("Security request-size and JWT clock-skew settings are invalid.");
}

builder.Services.AddApplication();
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();
builder.Services.AddFlashStreamInfrastructure(builder.Configuration);
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FlashStream API", Version = "v1" });
    options.OperationFilter<AuthorizeOperationFilter>();
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT issued by the configured identity provider."
    });
});
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authentication.Authority;
        options.Audience = authentication.Audience;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(jwtClockSkewSeconds)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
