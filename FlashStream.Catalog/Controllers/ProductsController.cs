using FlashStream.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Catalog.Controllers;

[ApiController]
[Route("products")]
public sealed class ProductsController(CatalogDbContext database) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> List(CancellationToken cancellationToken)
    {
        var products = await database.Products.AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new ProductResponse(item.Id, item.Name, item.Price))
            .ToListAsync(cancellationToken);
        return Ok(products);
    }

    [HttpGet("{productId:guid}", Name = "GetCatalogProduct")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Get(Guid productId, CancellationToken cancellationToken)
    {
        var product = await database.Products.AsNoTracking()
            .Where(item => item.Id == productId)
            .Select(item => new ProductResponse(item.Id, item.Name, item.Price))
            .SingleOrDefaultAsync(cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200 ||
            request.Price < 0 || !ModelState.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200
                    ? ["Name is required and must be at most 200 characters."]
                    : [],
                [nameof(request.Price)] = request.Price < 0
                    ? ["Price cannot be negative."]
                    : []
            }.Where(entry => entry.Value.Length > 0).ToDictionary()));
        }

        var product = new CatalogProduct
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Price = request.Price,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        database.Products.Add(product);
        await database.SaveChangesAsync(cancellationToken);

        var response = new ProductResponse(product.Id, product.Name, product.Price);
        return CreatedAtRoute("GetCatalogProduct", new { productId = product.Id }, response);
    }
}

public sealed record CreateProductRequest(string Name, decimal Price);
public sealed record ProductResponse(Guid Id, string Name, decimal Price);
