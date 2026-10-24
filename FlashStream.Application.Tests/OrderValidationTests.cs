using FlashStream.Api.Models;
using FlashStream.Api.Validation;
using FlashStream.Application.Configuration;
using FlashStream.Application.Contracts;
using FlashStream.Application.Validation;
using Microsoft.Extensions.Options;

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
        var request = new CreateOrderRequest(Guid.Empty, 11);

        var result = await validator.ValidateAsync(request);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.ProductId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.Quantity));
    }
}
