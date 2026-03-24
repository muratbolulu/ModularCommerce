using System.Text;
using System.Text.Json;
using Catalog.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared.Contracts.Events;

namespace Catalog.Infrastructure.Messaging;

public sealed class CatalogRabbitMqConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CatalogRabbitMqConsumer> _logger;
    private readonly ConnectionFactory _connectionFactory;
    private readonly string _exchange;
    private readonly string _createdQueue;
    private readonly string _updatedQueue;

    public CatalogRabbitMqConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<CatalogRabbitMqConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _exchange = configuration["RABBITMQ_EXCHANGE"] ?? "modularcommerce.product.events";
        _createdQueue = configuration["RABBITMQ_CATALOG_CREATED_QUEUE"] ?? "catalog.product.created.queue";
        _updatedQueue = configuration["RABBITMQ_CATALOG_UPDATED_QUEUE"] ?? "catalog.product.updated.queue";

        _connectionFactory = new ConnectionFactory
        {
            HostName = configuration["RABBITMQ_HOST"] ?? "localhost",
            Port = int.TryParse(configuration["RABBITMQ_PORT"], out var port) ? port : 5672,
            UserName = configuration["RABBITMQ_USER"] ?? "guest",
            Password = configuration["RABBITMQ_PASSWORD"] ?? "guest",
            VirtualHost = configuration["RABBITMQ_VHOST"] ?? "/",
            ClientProvidedName = "catalog-api-consumer"
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(_exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: stoppingToken);
            await channel.QueueDeclareAsync(_createdQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await channel.QueueDeclareAsync(_updatedQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await channel.QueueBindAsync(_createdQueue, _exchange, "product.created", cancellationToken: stoppingToken);
            await channel.QueueBindAsync(_updatedQueue, _exchange, "product.updated", cancellationToken: stoppingToken);

            _logger.LogInformation("Catalog RabbitMQ consumer started. Queues: {CreatedQueue}, {UpdatedQueue}", _createdQueue, _updatedQueue);

            while (!stoppingToken.IsCancellationRequested)
            {
                await ConsumeCreatedAsync(channel, stoppingToken);
                await ConsumeUpdatedAsync(channel, stoppingToken);
                await Task.Delay(250, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog RabbitMQ consumer crashed");
        }
    }

    private async Task ConsumeCreatedAsync(IChannel channel, CancellationToken cancellationToken)
    {
        var result = await channel.BasicGetAsync(_createdQueue, autoAck: false, cancellationToken);
        if (result is null)
        {
            return;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(result.Body.ToArray());
            var @event = JsonSerializer.Deserialize<ProductCreatedEvent>(payload);
            if (@event is not null)
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<ICatalogSagaOrchestrator>();
                await orchestrator.HandleProductCreatedAsync(@event, cancellationToken);
            }

            await channel.BasicAckAsync(result.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing product.created message");
            await channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
    }

    private async Task ConsumeUpdatedAsync(IChannel channel, CancellationToken cancellationToken)
    {
        var result = await channel.BasicGetAsync(_updatedQueue, autoAck: false, cancellationToken);
        if (result is null)
        {
            return;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(result.Body.ToArray());
            var @event = JsonSerializer.Deserialize<ProductUpdatedEvent>(payload);
            if (@event is not null)
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<ICatalogSagaOrchestrator>();
                await orchestrator.HandleProductUpdatedAsync(@event, cancellationToken);
            }

            await channel.BasicAckAsync(result.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing product.updated message");
            await channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
    }
}
