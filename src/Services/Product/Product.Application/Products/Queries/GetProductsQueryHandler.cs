using MediatR;
using Product.Application.Abstractions;
using Product.Application.Products.Mappers;
using Shared.Contracts.Products;

namespace Product.Application.Products.Queries;

public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, IReadOnlyCollection<ProductDto>>
{
    private readonly IProductRepository _repository;
    private readonly ICacheService _cacheService;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public GetProductsQueryHandler(IProductRepository repository, ICacheService cacheService)
    {
        _repository = repository;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyCollection<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var cached = await _cacheService.GetAsync<IReadOnlyCollection<ProductDto>>(CacheKeys.ProductsList, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var entities = await _repository.GetAllAsync(cancellationToken);
        var products = entities.Select(e => e.ToDto()).ToArray();
        await _cacheService.SetAsync(CacheKeys.ProductsList, products, CacheTtl, cancellationToken);

        return products;
    }
}
