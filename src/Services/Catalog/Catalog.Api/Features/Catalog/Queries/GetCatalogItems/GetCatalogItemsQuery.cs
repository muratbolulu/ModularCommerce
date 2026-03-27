using Catalog.Api.Features.Catalog.Shared;
using MediatR;

namespace Catalog.Api.Features.Catalog.Queries.GetCatalogItems;

public sealed record GetCatalogItemsQuery : IRequest<IReadOnlyCollection<CatalogItemView>>;
