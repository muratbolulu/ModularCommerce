namespace Product.Infrastructure.Messaging;

public interface IRabbitMqTopologyInitializer
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken);
}
