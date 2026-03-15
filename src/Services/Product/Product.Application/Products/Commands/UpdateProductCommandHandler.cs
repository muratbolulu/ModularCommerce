using MediatR;
using Product.Application.Abstractions;
using Product.Application.Products.Mappers;
using Shared.Contracts.Events;
using Shared.Contracts.Products;

namespace Product.Application.Products.Commands;

public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, ProductDto>
{
    private readonly IProductRepository _repository;
    private readonly ICacheService _cacheService;
    private readonly IEventPublisher _eventPublisher;

    public UpdateProductCommandHandler(
        IProductRepository repository,
        ICacheService cacheService,
        IEventPublisher eventPublisher)
    {
        _repository = repository;
        _cacheService = cacheService;
        _eventPublisher = eventPublisher;
    }

    public async Task<ProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.Id} not found.");

        entity.Update(request.Name, request.Price, request.Stock);
        await _repository.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeys.ProductsList, cancellationToken);

        await _eventPublisher.PublishAsync(
            new ProductUpdatedEvent(entity.Id, entity.Name, entity.Price, entity.Stock, DateTime.UtcNow),
            cancellationToken);

        return entity.ToDto();
    }
}
