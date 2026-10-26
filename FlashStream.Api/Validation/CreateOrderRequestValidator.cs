using FlashStream.Api.Models;
using FlashStream.Application.Configuration;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FlashStream.Api.Validation;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator(IOptions<OrderPolicySettings> policy)
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.ProductId).NotEmpty();
        RuleFor(request => request.Quantity)
            .InclusiveBetween(1, policy.Value.MaximumQuantity);
    }
}
