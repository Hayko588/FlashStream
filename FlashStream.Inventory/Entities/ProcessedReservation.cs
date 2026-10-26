namespace FlashStream.Inventory.Entities;

public sealed class ProcessedReservation
{
    public Guid EventId { get; set; }
    public Guid OrderId { get; set; }
    public bool Reserved { get; set; }
    public int RemainingInventory { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}
