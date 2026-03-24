using Shared.Contracts.Events;

namespace Catalog.Application.Abstractions;

public interface ICatalogCompensationEventPublisher
{
    Task PublishCatalogWriteFailedAsync(CatalogWriteFailedEvent @event, CancellationToken cancellationToken);
}
