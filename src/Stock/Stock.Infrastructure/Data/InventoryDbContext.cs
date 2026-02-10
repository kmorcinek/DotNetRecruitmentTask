using Microsoft.EntityFrameworkCore;
using Stock.Domain.Entities;

namespace Stock.Infrastructure.Data;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Inventory> Inventories { get; set; } = null!;
    public DbSet<ProductReadModel> ProductReadModels { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductId).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.AddedAt).IsRequired();
            entity.Property(e => e.AddedBy).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<ProductReadModel>(entity =>
        {
            entity.HasKey(e => e.ProductId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.SyncedAt).IsRequired();
        });
    }
}
