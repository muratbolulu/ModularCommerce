namespace Catalog.Api.Features.Catalog.Shared;

public sealed record CatalogItemView(Guid ProductId, string Name, decimal Price, int Stock, DateTime LastSyncedAtUtc);
