using FlashStream.Ordering.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Ordering;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<OrderRecord> Orders => Set<OrderRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderRecord>(entity =>
        {
            entity.HasKey(order => order.Id);
            entity.Property(order => order.UnitPrice).HasPrecision(18, 2);
            entity.Property(order => order.IdempotencyKey).HasMaxLength(100);
            entity.Property(order => order.Status).HasMaxLength(30);
            entity.HasIndex(order => new { order.UserId, order.IdempotencyKey }).IsUnique();
            entity.HasIndex(order => new { order.UserId, order.CreatedAt });
        });
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.PublishedAt, item.LockedUntil, item.CreatedAt });
        });
        modelBuilder.Entity<ProcessedEvent>().HasKey(item => item.EventId);
    }
}
