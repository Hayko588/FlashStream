namespace FlashStream.Inventory.Entities;

public sealed class InventoryItem
{
    public Guid ProductId { get; set; }
    public int AvailableQuantity { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
