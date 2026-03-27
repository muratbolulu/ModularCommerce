using Catalog.Application.Abstractions;
using Catalog.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events;

namespace Catalog.Api.Features.Catalog.Commands.SyncProductCreated;

public sealed class SyncProductCreatedCommandHandler : IRequestHandler<SyncProductCreatedCommand>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly ICatalogSagaRepository _sagaRepository;
    private readonly ICatalogCompensationEventPublisher _compensationEventPublisher;
    private readonly SyncProductCreatedCommandValidator _validator;
    private readonly ILogger<SyncProductCreatedCommandHandler> _logger;

    public SyncProductCreatedCommandHandler(
        ICatalogRepository catalogRepository,
        ICatalogSagaRepository sagaRepository,
        ICatalogCompensationEventPublisher compensationEventPublisher,
        SyncProductCreatedCommandValidator validator,
        ILogger<SyncProductCreatedCommandHandler> logger)
    {
        _catalogRepository = catalogRepository;
        _sagaRepository = sagaRepository;
        _compensationEventPublisher = compensationEventPublisher;
        _validator = validator;
        _logger = logger;
    }

    public async Task Handle(SyncProductCreatedCommand request, CancellationToken cancellationToken)
    {
        var @event = request.Event;
        var sagaKey = $"catalog-create-{@event.ProductId}";
        var existing = await _sagaRepository.GetBySagaKeyAsync(sagaKey, cancellationToken);
        if (existing is not null && existing.State == CatalogSagaState.Completed)
        {
            _logger.LogInformation("Catalog create saga already completed for product {ProductId}", @event.ProductId);
            return;
        }

        var saga = existing ?? new CatalogSagaInstance(sagaKey, @event.ProductId, nameof(ProductCreatedEvent));
        if (existing is null)
        {
            await _sagaRepository.AddAsync(saga, cancellationToken);
        }

        try
        {
            var errors = _validator.Validate(request);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", errors));
            }

            var item = new CatalogItem(@event.ProductId, @event.Name, @event.Price, @event.Stock);
            await _catalogRepository.UpsertAsync(item, cancellationToken);

            saga.MoveTo(CatalogSagaState.CatalogWriteCompleted);
            saga.MoveTo(CatalogSagaState.Completed);
            await _sagaRepository.UpdateAsync(saga, cancellationToken);
            _logger.LogInformation("Catalog create saga completed for product {ProductId}", @event.ProductId);
        }
        catch (Exception ex)
        {
            saga.MoveTo(CatalogSagaState.Compensating, ex.Message);
            await _sagaRepository.UpdateAsync(saga, cancellationToken);

            await _catalogRepository.RemoveByProductIdAsync(@event.ProductId, cancellationToken);
            saga.MoveTo(CatalogSagaState.Compensated, ex.Message);
            saga.MoveTo(CatalogSagaState.Failed, ex.Message);
            await _sagaRepository.UpdateAsync(saga, cancellationToken);
            await _compensationEventPublisher.PublishCatalogWriteFailedAsync(
                new CatalogWriteFailedEvent(
                    @event.ProductId,
                    sagaKey,
                    nameof(ProductCreatedEvent),
                    ex.Message,
                    DateTime.UtcNow),
                cancellationToken);

            _logger.LogError(ex, "Catalog create saga failed and compensated for product {ProductId}", @event.ProductId);
        }
    }
}
