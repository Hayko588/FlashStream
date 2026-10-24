using FlashStream.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlashStream.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("products")]
public sealed class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet("{productId:guid}", Name = "GetProduct")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid productId, CancellationToken cancellationToken)
    {
        var product = await productService.GetAsync(productId, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }
}
