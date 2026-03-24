namespace Catalog.Application.Catalog.QueryModels;

public sealed record CatalogItemView(Guid ProductId, string Name, decimal Price, int Stock, DateTime LastSyncedAtUtc);
