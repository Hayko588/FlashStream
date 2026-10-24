namespace FlashStream.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public required string Topic { get; set; }
    public required string Key { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
