namespace FlashStream.Domain.Events;

public sealed record OrderRequestedEvent(
    Guid EventId,
    Guid OrderId,
    Guid UserId,
    Guid ProductId,
    int Quantity,
    DateTimeOffset CreatedAt);

public sealed record OrderStateChangedEvent(
    Guid EventId,
    Guid OrderId,
    string Status,
    string? FailureReason,
    int? RemainingInventory,
    DateTimeOffset UpdatedAt);

public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Rejected = "Rejected";
}
