using FlashStream.Catalog.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlashStream.Catalog;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<CatalogProduct> Products => Set<CatalogProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogProduct>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Price).HasPrecision(18, 2);
            entity.ToTable(table =>
                table.HasCheckConstraint("CK_CatalogProducts_Price", "\"Price\" >= 0"));
        });
    }
}
