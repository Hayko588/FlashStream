using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Ordering.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(OrderingDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        await database.Database.CanConnectAsync(cancellationToken)
            ? Ok(new { status = "healthy" })
            : Problem("Ordering database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
}
