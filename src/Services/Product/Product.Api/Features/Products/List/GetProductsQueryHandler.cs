using MediatR;
using Microsoft.Extensions.Logging;
using Product.Api.Features.Products.Shared;
using Product.Application.Abstractions;
using Product.Application.Shared;
using Shared.Contracts.Products;

namespace Product.Api.Features.Products.List;

public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, IReadOnlyCollection<ProductDto>>
{
    private readonly IProductRepository _repository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetProductsQueryHandler> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public GetProductsQueryHandler(
        IProductRepository repository,
        ICacheService cacheService,
        ILogger<GetProductsQueryHandler> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var cached = await _cacheService.GetAsync<IReadOnlyCollection<ProductDto>>(ProductCacheKeys.ProductsList, cancellationToken);
        if (cached is not null)
        {
            _logger.LogInformation("Products fetched from cache. Count: {Count}", cached.Count);
            return cached;
        }

        _logger.LogInformation("Products cache miss. Fetching from database");
        var entities = await _repository.GetAllAsync(cancellationToken);
        var products = entities.Select(e => e.ToDto()).ToArray();
        await _cacheService.SetAsync(ProductCacheKeys.ProductsList, products, CacheTtl, cancellationToken);

        _logger.LogInformation("Products loaded from database and cached. Count: {Count}", products.Length);
        return products;
    }
}
