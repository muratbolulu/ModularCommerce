using Catalog.Domain.Entities;

namespace Catalog.Application.Abstractions;

public interface ICatalogRepository
{
    Task<CatalogItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task UpsertAsync(CatalogItem item, CancellationToken cancellationToken);
    Task RemoveByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CatalogItem>> ListAsync(CancellationToken cancellationToken);
}
