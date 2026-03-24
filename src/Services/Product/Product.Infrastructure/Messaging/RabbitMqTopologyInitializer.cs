using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Product.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializer : IRabbitMqTopologyInitializer
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger;
    private readonly string _exchangeName;
    private readonly string _createdQueue;
    private readonly string _updatedQueue;
    private readonly string _catalogWriteFailedQueue;

    public RabbitMqTopologyInitializer(IConfiguration configuration, ILogger<RabbitMqTopologyInitializer> logger)
    {
        _logger = logger;
        _exchangeName = configuration["RABBITMQ_EXCHANGE"] ?? "modularcommerce.product.events";
        _createdQueue = configuration["RABBITMQ_QUEUE_PRODUCT_CREATED"] ?? "product.created.queue";
        _updatedQueue = configuration["RABBITMQ_QUEUE_PRODUCT_UPDATED"] ?? "product.updated.queue";
        _catalogWriteFailedQueue = configuration["RABBITMQ_QUEUE_CATALOG_WRITE_FAILED"] ?? "product.catalog.write.failed.queue";

        _connectionFactory = new ConnectionFactory
        {
            HostName = configuration["RABBITMQ_HOST"] ?? "localhost",
            Port = int.TryParse(configuration["RABBITMQ_PORT"], out var port) ? port : 5672,
            UserName = configuration["RABBITMQ_USER"] ?? "guest",
            Password = configuration["RABBITMQ_PASSWORD"] ?? "guest",
            VirtualHost = configuration["RABBITMQ_VHOST"] ?? "/",
            ClientProvidedName = "product-api-topology-initializer"
        };
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: _exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(_createdQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
            await channel.QueueDeclareAsync(_updatedQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
            await channel.QueueDeclareAsync(_catalogWriteFailedQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);

            await channel.QueueBindAsync(_createdQueue, _exchangeName, "product.created", arguments: null, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(_updatedQueue, _exchangeName, "product.updated", arguments: null, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(_catalogWriteFailedQueue, _exchangeName, "catalog.write.failed", arguments: null, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "RabbitMQ topology ensured. Exchange: {Exchange}. Queues: {CreatedQueue}, {UpdatedQueue}, {CatalogWriteFailedQueue}",
                _exchangeName,
                _createdQueue,
                _updatedQueue,
                _catalogWriteFailedQueue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ topology initialization failed for exchange {Exchange}", _exchangeName);
        }
    }
}
