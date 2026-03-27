using Catalog.Api.Features.Catalog.Shared;
using Catalog.Application.Abstractions;
using MediatR;

namespace Catalog.Api.Features.Catalog.Queries.GetCatalogItems;

public sealed class GetCatalogItemsQueryHandler : IRequestHandler<GetCatalogItemsQuery, IReadOnlyCollection<CatalogItemView>>
{
    private readonly ICatalogRepository _catalogRepository;

    public GetCatalogItemsQueryHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<IReadOnlyCollection<CatalogItemView>> Handle(GetCatalogItemsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _catalogRepository.ListAsync(cancellationToken);
        return entities
            .Select(x => new CatalogItemView(x.ProductId, x.Name, x.Price, x.Stock, x.LastSyncedAtUtc))
            .ToArray();
    }
}
