using Catalog.Application.Abstractions;
using Catalog.Application.Catalog.QueryModels;

namespace Catalog.Application.Catalog;

public sealed class CatalogReadService
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly ICatalogSagaRepository _sagaRepository;

    public CatalogReadService(ICatalogRepository catalogRepository, ICatalogSagaRepository sagaRepository)
    {
        _catalogRepository = catalogRepository;
        _sagaRepository = sagaRepository;
    }

    public async Task<IReadOnlyCollection<CatalogItemView>> GetItemsAsync(CancellationToken cancellationToken)
    {
        var entities = await _catalogRepository.ListAsync(cancellationToken);
        return entities
            .Select(x => new CatalogItemView(x.ProductId, x.Name, x.Price, x.Stock, x.LastSyncedAtUtc))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<CatalogSagaView>> GetSagaStatesAsync(CancellationToken cancellationToken)
    {
        var sagas = await _sagaRepository.ListAsync(cancellationToken);
        return sagas
            .Select(x => new CatalogSagaView(x.SagaKey, x.ProductId, x.EventType, x.State, x.ErrorMessage, x.UpdatedAtUtc))
            .ToArray();
    }
}
