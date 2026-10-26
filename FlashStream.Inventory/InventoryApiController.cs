using FlashStream.Inventory.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Inventory;

[ApiController]
[Route("inventory")]
public sealed class InventoryApiController(InventoryDbContext database) : ControllerBase
{
    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> Get(Guid productId, CancellationToken cancellationToken)
    {
        var item = await database.Inventory.AsNoTracking()
            .SingleOrDefaultAsync(value => value.ProductId == productId, cancellationToken);
        return item is null
            ? NotFound()
            : Ok(new InventoryResponse(item.ProductId, item.AvailableQuantity));
    }

    [HttpPut("{productId:guid}")]
    public async Task<IActionResult> Set(
        Guid productId,
        SetInventoryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AvailableQuantity < 0)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.AvailableQuantity)] = ["Available quantity cannot be negative."]
            }));
        }

        var item = await database.Inventory.FindAsync([productId], cancellationToken);
        if (item is null)
        {
            item = new InventoryItem { ProductId = productId };
            database.Inventory.Add(item);
        }

        item.AvailableQuantity = request.AvailableQuantity;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return Ok(new InventoryResponse(productId, item.AvailableQuantity));
    }
}

public sealed record SetInventoryRequest(int AvailableQuantity);
public sealed record InventoryResponse(Guid ProductId, int AvailableQuantity);
