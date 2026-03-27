using MediatR;
using Microsoft.Extensions.Logging;
using Product.Api.Features.Products.Shared;
using Product.Application.Abstractions;
using Product.Application.Shared;
using Shared.Contracts.Events;
using Shared.Contracts.Products;

namespace Product.Api.Features.Products.Update;

public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, ProductDto>
{
    private readonly IProductRepository _repository;
    private readonly ICacheService _cacheService;
    private readonly IEventPublisher _eventPublisher;
    private readonly UpdateProductCommandValidator _validator;
    private readonly ILogger<UpdateProductCommandHandler> _logger;

    public UpdateProductCommandHandler(
        IProductRepository repository,
        ICacheService cacheService,
        IEventPublisher eventPublisher,
        UpdateProductCommandValidator validator,
        ILogger<UpdateProductCommandHandler> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _eventPublisher = eventPublisher;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(request);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        _logger.LogInformation("Updating product {ProductId}", request.Id);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.Id} not found.");

        entity.Update(request.Name, request.Price, request.Stock);
        await _repository.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(ProductCacheKeys.ProductsList, cancellationToken);

        await _eventPublisher.PublishAsync(
            new ProductUpdatedEvent(entity.Id, entity.Name, entity.Price, entity.Stock, DateTime.UtcNow),
            cancellationToken);

        _logger.LogInformation("Product {ProductId} updated and cache invalidated", entity.Id);
        return entity.ToDto();
    }
}
