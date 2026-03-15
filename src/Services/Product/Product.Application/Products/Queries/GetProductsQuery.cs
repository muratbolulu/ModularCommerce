using MediatR;
using Shared.Contracts.Products;

namespace Product.Application.Products.Queries;

public sealed record GetProductsQuery : IRequest<IReadOnlyCollection<ProductDto>>;
