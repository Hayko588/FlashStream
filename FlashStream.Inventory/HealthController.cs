using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Inventory;

[ApiController]
[Route("health")]
public sealed class HealthController(InventoryDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        await database.Database.CanConnectAsync(cancellationToken)
            ? Ok(new { status = "healthy" })
            : Problem("Inventory database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
}
