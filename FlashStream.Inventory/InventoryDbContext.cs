using FlashStream.Inventory.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Inventory;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
    public DbSet<ProcessedReservation> ProcessedReservations => Set<ProcessedReservation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(item => item.ProductId);
            entity.ToTable(table =>
                table.HasCheckConstraint("CK_Inventory_AvailableQuantity", "\"AvailableQuantity\" >= 0"));
        });
        modelBuilder.Entity<ProcessedReservation>().HasKey(item => item.EventId);
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.PublishedAt, item.LockedUntil, item.CreatedAt });
        });
    }
}
