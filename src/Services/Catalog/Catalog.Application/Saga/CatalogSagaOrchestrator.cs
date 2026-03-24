using Catalog.Application.Abstractions;
using Catalog.Domain.Entities;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events;

namespace Catalog.Application.Saga;

public sealed class CatalogSagaOrchestrator : ICatalogSagaOrchestrator
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly ICatalogSagaRepository _sagaRepository;
    private readonly ILogger<CatalogSagaOrchestrator> _logger;

    public CatalogSagaOrchestrator(
        ICatalogRepository catalogRepository,
        ICatalogSagaRepository sagaRepository,
        ILogger<CatalogSagaOrchestrator> logger)
    {
        _catalogRepository = catalogRepository;
        _sagaRepository = sagaRepository;
        _logger = logger;
    }

    public async Task HandleProductCreatedAsync(ProductCreatedEvent @event, CancellationToken cancellationToken)
    {
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
            Validate(@event.Name, @event.Price);

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

            _logger.LogError(ex, "Catalog create saga failed and compensated for product {ProductId}", @event.ProductId);
        }
    }

    public async Task HandleProductUpdatedAsync(ProductUpdatedEvent @event, CancellationToken cancellationToken)
    {
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
            Validate(@event.Name, @event.Price);

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
            _logger.LogError(ex, "Catalog update saga failed and compensated for product {ProductId}", @event.ProductId);
        }
    }

    private static void Validate(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Catalog item name cannot be empty.");
        }

        if (price < 0)
        {
            throw new InvalidOperationException("Catalog item price cannot be negative.");
        }
    }
}
