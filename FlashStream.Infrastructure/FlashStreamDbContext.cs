using FlashStream.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Infrastructure;

public sealed class FlashStreamDbContext(DbContextOptions<FlashStreamDbContext> options)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Price).HasPrecision(18, 2);
            entity.ToTable(table => table.HasCheckConstraint("CK_Products_Inventory", "\"Inventory\" >= 0"));
        });

        modelBuilder.Entity<Order>(entity =>
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
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Topic).HasMaxLength(200);
            entity.Property(message => message.Key).HasMaxLength(200);
            entity.HasIndex(message => new { message.PublishedAt, message.LockedUntil, message.CreatedAt });
        });

        modelBuilder.Entity<ProcessedEvent>(entity => entity.HasKey(processed => processed.EventId));
    }
}
