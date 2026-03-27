using MediatR;
using Microsoft.Extensions.Logging;
using Product.Api.Features.Products.Shared;
using Product.Application.Abstractions;
using Product.Application.Shared;
using Product.Domain.Entities;
using Shared.Contracts.Events;
using Shared.Contracts.Products;

namespace Product.Api.Features.Products.Create;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IProductRepository _repository;
    private readonly ICacheService _cacheService;
    private readonly IEventPublisher _eventPublisher;
    private readonly CreateProductCommandValidator _validator;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(
        IProductRepository repository,
        ICacheService cacheService,
        IEventPublisher eventPublisher,
        CreateProductCommandValidator validator,
        ILogger<CreateProductCommandHandler> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _eventPublisher = eventPublisher;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(request);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        _logger.LogInformation(
            "Creating product {Name} with price {Price} and stock {Stock}",
            request.Name,
            request.Price,
            request.Stock);

        var entity = new ProductEntity(request.Name, request.Price, request.Stock);
        await _repository.AddAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(ProductCacheKeys.ProductsList, cancellationToken);

        await _eventPublisher.PublishAsync(
            new ProductCreatedEvent(entity.Id, entity.Name, entity.Price, entity.Stock, DateTime.UtcNow),
            cancellationToken);

        _logger.LogInformation("Product {ProductId} created and cache invalidated", entity.Id);
        return entity.ToDto();
    }
}
