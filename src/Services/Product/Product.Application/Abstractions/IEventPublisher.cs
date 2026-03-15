namespace Product.Application.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken);
}
