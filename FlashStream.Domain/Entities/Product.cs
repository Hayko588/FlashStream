namespace FlashStream.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public int Inventory { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
