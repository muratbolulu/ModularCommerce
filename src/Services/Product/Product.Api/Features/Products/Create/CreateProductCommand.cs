using MediatR;
using Shared.Contracts.Products;

namespace Product.Api.Features.Products.Create;

public sealed record CreateProductCommand(string Name, decimal Price, int Stock) : IRequest<ProductDto>;
