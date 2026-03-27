using MediatR;
using Shared.Contracts.Products;

namespace Product.Api.Features.Products.Update;

public sealed record UpdateProductCommand(Guid Id, string Name, decimal Price, int Stock) : IRequest<ProductDto>;
