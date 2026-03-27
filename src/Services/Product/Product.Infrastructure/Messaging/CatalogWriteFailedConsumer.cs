using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Product.Application.Abstractions;
using Product.Application.Shared;
using RabbitMQ.Client;
using Shared.Contracts.Events;

namespace Product.Infrastructure.Messaging;

public sealed class CatalogWriteFailedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CatalogWriteFailedConsumer> _logger;
    private readonly ConnectionFactory _connectionFactory;
    private readonly string _exchangeName;
    private readonly string _catalogWriteFailedQueue;

    public CatalogWriteFailedConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<CatalogWriteFailedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _exchangeName = configuration["RABBITMQ_EXCHANGE"] ?? "modularcommerce.product.events";
        _catalogWriteFailedQueue = configuration["RABBITMQ_QUEUE_CATALOG_WRITE_FAILED"] ?? "product.catalog.write.failed.queue";

        _connectionFactory = new ConnectionFactory
        {
            HostName = configuration["RABBITMQ_HOST"] ?? "localhost",
            Port = int.TryParse(configuration["RABBITMQ_PORT"], out var port) ? port : 5672,
            UserName = configuration["RABBITMQ_USER"] ?? "guest",
            Password = configuration["RABBITMQ_PASSWORD"] ?? "guest",
            VirtualHost = configuration["RABBITMQ_VHOST"] ?? "/",
            ClientProvidedName = "product-api-catalog-write-failed-consumer"
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(
                exchange: _exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(_catalogWriteFailedQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await channel.QueueBindAsync(_catalogWriteFailedQueue, _exchangeName, "catalog.write.failed", cancellationToken: stoppingToken);

            _logger.LogInformation("Catalog write failed consumer started. Queue: {Queue}", _catalogWriteFailedQueue);

            while (!stoppingToken.IsCancellationRequested)
            {
                var result = await channel.BasicGetAsync(_catalogWriteFailedQueue, autoAck: false, cancellationToken: stoppingToken);
                if (result is null)
                {
                    await Task.Delay(250, stoppingToken);
                    continue;
                }

                try
                {
                    var payload = Encoding.UTF8.GetString(result.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<CatalogWriteFailedEvent>(payload);

                    if (@event is null)
                    {
                        _logger.LogWarning("Catalog write failed event deserialization returned null");
                        await channel.BasicAckAsync(result.DeliveryTag, multiple: false, stoppingToken);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                    var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                    var deleted = await productRepository.SoftDeleteAsync(@event.ProductId, stoppingToken);
                    await cacheService.RemoveAsync(ProductCacheKeys.ProductsList, stoppingToken);

                    if (deleted)
                    {
                        _logger.LogWarning(
                            "Soft-deleted product {ProductId} due to catalog write failure. SagaKey: {SagaKey}, SourceEvent: {SourceEventType}, Reason: {Reason}",
                            @event.ProductId,
                            @event.SagaKey,
                            @event.SourceEventType,
                            @event.Reason);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Catalog write failed event received but product {ProductId} not found. SagaKey: {SagaKey}",
                            @event.ProductId,
                            @event.SagaKey);
                    }

                    await channel.BasicAckAsync(result.DeliveryTag, multiple: false, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process catalog.write.failed message");
                    await channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog write failed consumer crashed");
        }
    }
}
