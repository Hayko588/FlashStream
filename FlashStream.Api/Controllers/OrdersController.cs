using System.Security.Claims;
using FlashStream.Api.Models;
using FlashStream.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FlashStream.Api.Controllers;

[ApiController]
[Authorize]
[Route("orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost(Name = "CreateOrder")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        [BindRequired, FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(
            GetAuthenticatedUserId(),
            request.ProductId,
            request.Quantity,
            idempotencyKey.ToString());
        var result = await orderService.CreateAsync(command, cancellationToken);
        return result.Created
            ? AcceptedAtRoute("GetOrder", new { orderId = result.Order.Id }, result.Order)
            : Ok(result.Order);
    }

    [HttpGet("{orderId:guid}", Name = "GetOrder")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orderService.GetAsync(orderId, GetAuthenticatedUserId(), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    private Guid GetAuthenticatedUserId()
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : throw new InvalidUserClaimException();
    }
}
