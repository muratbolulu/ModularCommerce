using Catalog.Domain.Entities;

namespace Catalog.Application.Abstractions;

public interface ICatalogSagaRepository
{
    Task<CatalogSagaInstance?> GetBySagaKeyAsync(string sagaKey, CancellationToken cancellationToken);
    Task AddAsync(CatalogSagaInstance saga, CancellationToken cancellationToken);
    Task UpdateAsync(CatalogSagaInstance saga, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CatalogSagaInstance>> ListAsync(CancellationToken cancellationToken);
}
