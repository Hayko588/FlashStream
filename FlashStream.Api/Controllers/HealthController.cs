using FlashStream.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FlashStream.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IHealthService healthService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var health = await healthService.CheckAsync(cancellationToken);
        return health.DatabaseAvailable
            ? Ok(new { status = "healthy" })
            : Problem("Database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
