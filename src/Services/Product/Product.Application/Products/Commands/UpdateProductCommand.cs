using MediatR;
using Shared.Contracts.Products;

namespace Product.Application.Products.Commands;

public sealed record UpdateProductCommand(Guid Id, string Name, decimal Price, int Stock) : IRequest<ProductDto>;
