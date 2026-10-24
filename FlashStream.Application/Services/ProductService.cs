using FlashStream.Application.Contracts;

namespace FlashStream.Application.Services;

public sealed class ProductService(IProductRepository products, IProductCache cache) : IProductService
{
    public async Task<ProductResponse?> GetAsync(Guid productId, CancellationToken cancellationToken)
    {
        var cachedProduct = await cache.GetAsync(productId, cancellationToken);
        if (cachedProduct is not null)
        {
            return cachedProduct;
        }

        var product = await products.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        await cache.SetAsync(product, cancellationToken);
        return new ProductResponse(product.Id, product.Name, product.Price, product.Inventory);
    }
}
