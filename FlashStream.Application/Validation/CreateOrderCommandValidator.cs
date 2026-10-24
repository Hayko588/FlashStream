using FlashStream.Application.Configuration;
using FlashStream.Application.Contracts;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FlashStream.Application.Validation;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator(IOptions<OrderPolicySettings> policy)
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.ProductId).NotEmpty();
        RuleFor(command => command.Quantity)
            .InclusiveBetween(1, policy.Value.MaximumQuantity);
        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
            .WithMessage("A valid GUID Idempotency-Key header is required.");
    }
}
