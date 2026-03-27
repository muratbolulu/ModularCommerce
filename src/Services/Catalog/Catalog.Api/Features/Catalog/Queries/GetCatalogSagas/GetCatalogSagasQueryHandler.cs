using Catalog.Api.Features.Catalog.Shared;
using Catalog.Application.Abstractions;
using MediatR;

namespace Catalog.Api.Features.Catalog.Queries.GetCatalogSagas;

public sealed class GetCatalogSagasQueryHandler : IRequestHandler<GetCatalogSagasQuery, IReadOnlyCollection<CatalogSagaView>>
{
    private readonly ICatalogSagaRepository _sagaRepository;

    public GetCatalogSagasQueryHandler(ICatalogSagaRepository sagaRepository)
    {
        _sagaRepository = sagaRepository;
    }

    public async Task<IReadOnlyCollection<CatalogSagaView>> Handle(GetCatalogSagasQuery request, CancellationToken cancellationToken)
    {
        var sagas = await _sagaRepository.ListAsync(cancellationToken);
        return sagas
            .Select(x => new CatalogSagaView(x.SagaKey, x.ProductId, x.EventType, x.State, x.ErrorMessage, x.UpdatedAtUtc))
            .ToArray();
    }
}
