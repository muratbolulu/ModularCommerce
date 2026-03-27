using Catalog.Api.Features.Catalog.Shared;
using MediatR;

namespace Catalog.Api.Features.Catalog.Queries.GetCatalogSagas;

public sealed record GetCatalogSagasQuery : IRequest<IReadOnlyCollection<CatalogSagaView>>;
