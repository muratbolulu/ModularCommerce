using Product.Domain.Entities;
using Shared.Contracts.Products;

namespace Product.Api.Features.Products.Shared;

internal static class ProductMappings
{
    public static ProductDto ToDto(this ProductEntity entity)
        => new(entity.Id, entity.Name, entity.Price, entity.Stock, entity.CreatedAtUtc);
}
