using FlashStream.Api.Models;
using FlashStream.Api.Validation;
using FlashStream.Application.Configuration;
using FlashStream.Application.Contracts;
using FlashStream.Application.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace FlashStream.Application.Tests;

public sealed class OrderValidationTests
{
    private static readonly OrderPolicySettings OrderPolicy = new() { MaximumQuantity = 10 };

    [Fact]
    public async Task CommandValidatorRejectsInvalidQuantityAndIdempotencyKey()
    {
        var validator = new CreateOrderCommandValidator(Options.Create(OrderPolicy));
        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), 11, Guid.Empty.ToString());

        var result = await validator.ValidateAsync(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Quantity));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.IdempotencyKey));
    }

    [Fact]
    public async Task CommandValidatorAcceptsAValidOrder()
    {
        var validator = new CreateOrderCommandValidator(Options.Create(OrderPolicy));
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10,
            Guid.NewGuid().ToString());

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ApiRequestValidatorRejectsEmptyProductAndOutOfPolicyQuantity()
    {
        var validator = new CreateOrderRequestValidator(Options.Create(OrderPolicy));
        var request = new CreateOrderRequest(Guid.Empty, Guid.Empty, 11);

        var result = await validator.ValidateAsync(request);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.UserId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.ProductId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.Quantity));
    }

    [Fact]
    public void MvcRecordValidationMetadataDoesNotThrow()
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .AddControllers()
            .Services
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var validator = services.GetRequiredService<IObjectModelValidator>();

        validator.Validate(
            actionContext,
            new ValidationStateDictionary(),
            string.Empty,
            new CreateOrderRequest(Guid.NewGuid(), Guid.NewGuid(), 1));
    }
}
