using MediatR;
using Shared.Contracts.Products;

namespace Product.Application.Products.Commands;

public sealed record CreateProductCommand(string Name, decimal Price, int Stock) : IRequest<ProductDto>;
