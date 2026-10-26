namespace FlashStream.Catalog.Entities;

public sealed class CatalogProduct
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
