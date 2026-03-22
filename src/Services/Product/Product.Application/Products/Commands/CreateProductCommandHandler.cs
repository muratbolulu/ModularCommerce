using MediatR;
using Microsoft.Extensions.Logging;
using Product.Application.Abstractions;
using Product.Application.Products.Mappers;
using Product.Domain.Entities;
using Shared.Contracts.Events;
using Shared.Contracts.Products;

namespace Product.Application.Products.Commands;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IProductRepository _repository;
    private readonly ICacheService _cacheService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(
        IProductRepository repository,
        ICacheService cacheService,
        IEventPublisher eventPublisher,
        ILogger<CreateProductCommandHandler> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating product {Name} with price {Price} and stock {Stock}",
            request.Name,
            request.Price,
            request.Stock);

        var entity = new ProductEntity(request.Name, request.Price, request.Stock);
        await _repository.AddAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeys.ProductsList, cancellationToken);

        await _eventPublisher.PublishAsync(
            new ProductCreatedEvent(entity.Id, entity.Name, entity.Price, entity.Stock, DateTime.UtcNow),
            cancellationToken);

        _logger.LogInformation("Product {ProductId} created and cache invalidated", entity.Id);
        return entity.ToDto();
    }
}
