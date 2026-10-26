using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Catalog.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(CatalogDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        await database.Database.CanConnectAsync(cancellationToken)
            ? Ok(new { status = "healthy" })
            : Problem("Catalog database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
}
