using MediatR;
using Shared.Contracts.Products;

namespace Product.Api.Features.Products.List;

public sealed record GetProductsQuery : IRequest<IReadOnlyCollection<ProductDto>>;
