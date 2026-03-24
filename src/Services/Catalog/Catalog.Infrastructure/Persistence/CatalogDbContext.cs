using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<CatalogSagaInstance> CatalogSagas => Set<CatalogSagaInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogItem>(cfg =>
        {
            cfg.ToTable("CatalogItems");
            cfg.HasKey(x => x.Id);
            cfg.HasIndex(x => x.ProductId).IsUnique();
            cfg.Property(x => x.Name).HasMaxLength(200).IsRequired();
            cfg.Property(x => x.Price).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CatalogSagaInstance>(cfg =>
        {
            cfg.ToTable("CatalogSagas");
            cfg.HasKey(x => x.Id);
            cfg.HasIndex(x => x.SagaKey).IsUnique();
            cfg.Property(x => x.SagaKey).HasMaxLength(200).IsRequired();
            cfg.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            cfg.Property(x => x.State).HasConversion<string>().HasMaxLength(50);
            cfg.Property(x => x.ErrorMessage).HasMaxLength(1000);
        });
    }
}
