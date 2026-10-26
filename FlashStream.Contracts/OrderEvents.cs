namespace FlashStream.Contracts;

public sealed record InventoryReservationRequested(
    Guid EventId,
    Guid OrderId,
    Guid UserId,
    Guid ProductId,
    int Quantity,
    DateTimeOffset RequestedAt);

public sealed record InventoryReservationCompleted(
    Guid EventId,
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    bool Reserved,
    int RemainingInventory,
    string? RejectionReason,
    DateTimeOffset CompletedAt);

public static class OrderTopics
{
    public const string InventoryReservationRequests = "inventory-reservation-requests";
    public const string InventoryReservationResults = "inventory-reservation-results";
}
