using FlashStream.Application.Configuration;
using FlashStream.Application.Contracts;
using FlashStream.Application.Services;
using FlashStream.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FlashStream.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateOrderCommandValidator>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IHealthService, HealthService>();
        return services;
    }
}
