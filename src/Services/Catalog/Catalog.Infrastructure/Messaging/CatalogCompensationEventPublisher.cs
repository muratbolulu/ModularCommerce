using System.Text;
using System.Text.Json;
using Catalog.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared.Contracts.Events;

namespace Catalog.Infrastructure.Messaging;

public sealed class CatalogCompensationEventPublisher : ICatalogCompensationEventPublisher
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<CatalogCompensationEventPublisher> _logger;
    private readonly string _exchangeName;

    public CatalogCompensationEventPublisher(IConfiguration configuration, ILogger<CatalogCompensationEventPublisher> logger)
    {
        _logger = logger;
        _exchangeName = configuration["RABBITMQ_EXCHANGE"] ?? "modularcommerce.product.events";

        _connectionFactory = new ConnectionFactory
        {
            HostName = configuration["RABBITMQ_HOST"] ?? "localhost",
            Port = int.TryParse(configuration["RABBITMQ_PORT"], out var port) ? port : 5672,
            UserName = configuration["RABBITMQ_USER"] ?? "guest",
            Password = configuration["RABBITMQ_PASSWORD"] ?? "guest",
            VirtualHost = configuration["RABBITMQ_VHOST"] ?? "/",
            ClientProvidedName = "catalog-api-compensation-publisher"
        };
    }

    public async Task PublishCatalogWriteFailedAsync(CatalogWriteFailedEvent @event, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(payload);

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

            await channel.BasicPublishAsync(
                exchange: _exchangeName,
                routingKey: "catalog.write.failed",
                mandatory: false,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogWarning(
                "Published compensation event catalog.write.failed for product {ProductId} and saga {SagaKey}",
                @event.ProductId,
                @event.SagaKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish catalog.write.failed for product {ProductId}", @event.ProductId);
        }
    }
}
