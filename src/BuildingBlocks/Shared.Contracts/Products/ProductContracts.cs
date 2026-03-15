namespace Shared.Contracts.Products;

public sealed record ProductDto(Guid Id, string Name, decimal Price, int Stock, DateTime CreatedAtUtc);

public sealed record CreateProductRequest(string Name, decimal Price, int Stock);

public sealed record UpdateProductRequest(string Name, decimal Price, int Stock);
