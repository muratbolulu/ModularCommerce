using Microsoft.EntityFrameworkCore;
using Product.Domain.Entities;

namespace Product.Infrastructure.Persistence;

public sealed class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options)
    {
    }

    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductEntity>(cfg =>
        {
            cfg.ToTable("Products");
            cfg.HasKey(x => x.Id);
            cfg.Property(x => x.Name).HasMaxLength(200).IsRequired();
            cfg.Property(x => x.Price).HasPrecision(18, 2);
            cfg.Property(x => x.Stock).IsRequired();
            cfg.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
            cfg.Property(x => x.IsDeleted).IsRequired();
            cfg.Property(x => x.DeletedAtUtc).IsRequired(false);
            cfg.Property(x => x.CreatedAtUtc).IsRequired();
            cfg.Property(x => x.UpdatedAtUtc).IsRequired();
        });
    }
}
