namespace Catalog.Domain.Entities;

public sealed class CatalogItem
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public DateTime LastSyncedAtUtc { get; private set; }

    private CatalogItem()
    {
    }

    public CatalogItem(Guid productId, string name, decimal price, int stock)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        Name = name;
        Price = price;
        Stock = stock;
        LastSyncedAtUtc = DateTime.UtcNow;
    }

    public void Update(string name, decimal price, int stock)
    {
        Name = name;
        Price = price;
        Stock = stock;
        LastSyncedAtUtc = DateTime.UtcNow;
    }
}
