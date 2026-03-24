using Shared.Contracts.Events;

namespace Catalog.Application.Abstractions;

public interface ICatalogSagaOrchestrator
{
    Task HandleProductCreatedAsync(ProductCreatedEvent @event, CancellationToken cancellationToken);
    Task HandleProductUpdatedAsync(ProductUpdatedEvent @event, CancellationToken cancellationToken);
}
