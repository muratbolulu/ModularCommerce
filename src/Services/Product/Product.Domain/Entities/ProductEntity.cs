namespace Product.Domain.Entities;

public sealed class ProductEntity
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ProductEntity()
    {
    }

    public ProductEntity(string name, decimal price, int stock)
    {
        Id = Guid.NewGuid();
        Name = name;
        Price = price;
        Stock = stock;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(string name, decimal price, int stock)
    {
        Name = name;
        Price = price;
        Stock = stock;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
