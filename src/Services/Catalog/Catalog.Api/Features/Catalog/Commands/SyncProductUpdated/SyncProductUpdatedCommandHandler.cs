using Catalog.Application.Abstractions;
using Catalog.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events;

namespace Catalog.Api.Features.Catalog.Commands.SyncProductUpdated;

public sealed class SyncProductUpdatedCommandHandler : IRequestHandler<SyncProductUpdatedCommand>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly ICatalogSagaRepository _sagaRepository;
    private readonly ICatalogCompensationEventPublisher _compensationEventPublisher;
    private readonly SyncProductUpdatedCommandValidator _validator;
    private readonly ILogger<SyncProductUpdatedCommandHandler> _logger;

    public SyncProductUpdatedCommandHandler(
        ICatalogRepository catalogRepository,
        ICatalogSagaRepository sagaRepository,
        ICatalogCompensationEventPublisher compensationEventPublisher,
        SyncProductUpdatedCommandValidator validator,
        ILogger<SyncProductUpdatedCommandHandler> logger)
    {
        _catalogRepository = catalogRepository;
        _sagaRepository = sagaRepository;
        _compensationEventPublisher = compensationEventPublisher;
        _validator = validator;
        _logger = logger;
    }

    public async Task Handle(SyncProductUpdatedCommand request, CancellationToken cancellationToken)
    {
        var @event = request.Event;
        var sagaKey = $"catalog-update-{@event.ProductId}";
        var existingSaga = await _sagaRepository.GetBySagaKeyAsync(sagaKey, cancellationToken);
        if (existingSaga is not null && existingSaga.State == CatalogSagaState.Completed)
        {
            _logger.LogInformation("Catalog update saga already completed for product {ProductId}", @event.ProductId);
            return;
        }

        var saga = existingSaga ?? new CatalogSagaInstance(sagaKey, @event.ProductId, nameof(ProductUpdatedEvent));
        if (existingSaga is null)
        {
            await _sagaRepository.AddAsync(saga, cancellationToken);
        }

        CatalogItem? backup = null;
        try
        {
            var errors = _validator.Validate(request);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", errors));
            }

            var current = await _catalogRepository.GetByProductIdAsync(@event.ProductId, cancellationToken);
            if (current is not null)
            {
                backup = new CatalogItem(current.ProductId, current.Name, current.Price, current.Stock);
            }

            var target = current ?? new CatalogItem(@event.ProductId, @event.Name, @event.Price, @event.Stock);
            target.Update(@event.Name, @event.Price, @event.Stock);
            await _catalogRepository.UpsertAsync(target, cancellationToken);

            saga.MoveTo(CatalogSagaState.CatalogWriteCompleted);
            saga.MoveTo(CatalogSagaState.Completed);
            await _sagaRepository.UpdateAsync(saga, cancellationToken);
            _logger.LogInformation("Catalog update saga completed for product {ProductId}", @event.ProductId);
        }
        catch (Exception ex)
        {
            saga.MoveTo(CatalogSagaState.Compensating, ex.Message);
            await _sagaRepository.UpdateAsync(saga, cancellationToken);

            if (backup is not null)
            {
                await _catalogRepository.UpsertAsync(backup, cancellationToken);
            }

            saga.MoveTo(CatalogSagaState.Compensated, ex.Message);
            saga.MoveTo(CatalogSagaState.Failed, ex.Message);
            await _sagaRepository.UpdateAsync(saga, cancellationToken);
            await _compensationEventPublisher.PublishCatalogWriteFailedAsync(
                new CatalogWriteFailedEvent(
                    @event.ProductId,
                    sagaKey,
                    nameof(ProductUpdatedEvent),
                    ex.Message,
                    DateTime.UtcNow),
                cancellationToken);

            _logger.LogError(ex, "Catalog update saga failed and compensated for product {ProductId}", @event.ProductId);
        }
    }
}
