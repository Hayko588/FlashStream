using System.Text.Json;
using Confluent.Kafka;
using FlashStream.Contracts;
using FlashStream.Ordering.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Ordering.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(
    OrderingDbContext database,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.ProductId == Guid.Empty ||
            request.Quantity < 1 || request.Quantity > configuration.GetValue<int>("Order:MaximumQuantity"))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["request"] = ["UserId, ProductId, and Quantity are invalid."]
            }));
        }

        if (idempotencyKey == Guid.Empty)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["A non-empty GUID idempotency key is required."]
            }));
        }

        var normalizedKey = idempotencyKey.ToString("N");
        var existing = await database.Orders.AsNoTracking().SingleOrDefaultAsync(
            order => order.UserId == request.UserId && order.IdempotencyKey == normalizedKey,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.ProductId != request.ProductId || existing.Quantity != request.Quantity)
            {
                return Conflict(new { error = "Idempotency key was already used for a different order." });
            }

            return Ok(ToResponse(existing));
        }

        var catalog = httpClientFactory.CreateClient("catalog");
        using var catalogResponse = await catalog.GetAsync(
            $"products/{request.ProductId}",
            cancellationToken);
        if (catalogResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(new { error = "Product was not found in Catalog." });
        }

        catalogResponse.EnsureSuccessStatusCode();
        var product = await catalogResponse.Content.ReadFromJsonAsync<CatalogProductResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Catalog returned an empty product response.");

        var now = DateTimeOffset.UtcNow;
        var order = new OrderRecord
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            ProductId = product.Id,
            Quantity = request.Quantity,
            UnitPrice = product.Price,
            IdempotencyKey = normalizedKey,
            Status = "PendingInventory",
            CreatedAt = now,
            UpdatedAt = now
        };
        var reservationRequested = new InventoryReservationRequested(
            Guid.NewGuid(),
            order.Id,
            order.UserId,
            order.ProductId,
            order.Quantity,
            now);

        database.Orders.Add(order);
        database.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = configuration["Kafka:InventoryReservationRequestsTopic"]
                ?? throw new InvalidOperationException("Kafka request topic is required."),
            Key = order.Id.ToString(),
            Payload = JsonSerializer.Serialize(reservationRequested, JsonSerializerOptions.Web),
            CreatedAt = now
        });
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            existing = await database.Orders.AsNoTracking().SingleOrDefaultAsync(
                item => item.UserId == request.UserId && item.IdempotencyKey == normalizedKey,
                cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return existing.ProductId == request.ProductId && existing.Quantity == request.Quantity
                ? Ok(ToResponse(existing))
                : Conflict(new { error = "Idempotency key was already used for a different order." });
        }

        return AcceptedAtAction(nameof(Get), new { orderId = order.Id }, ToResponse(order));
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderResponse>> Get(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await database.Orders.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        return order is null ? NotFound() : Ok(ToResponse(order));
    }

    private static OrderResponse ToResponse(OrderRecord order) =>
        new(order.Id, order.UserId, order.ProductId, order.Quantity, order.UnitPrice, order.Status,
            order.FailureReason);
}

public sealed record CreateOrderRequest(Guid UserId, Guid ProductId, int Quantity);
public sealed record OrderResponse(
    Guid Id,
    Guid UserId,
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    string Status,
    string? FailureReason);
public sealed record CatalogProductResponse(Guid Id, string Name, decimal Price);
