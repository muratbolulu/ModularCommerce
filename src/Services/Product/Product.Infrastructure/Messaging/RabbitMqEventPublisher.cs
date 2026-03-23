using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Product.Application.Abstractions;
using RabbitMQ.Client;
using Shared.Contracts.Events;

namespace Product.Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher : IEventPublisher
{
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly ConnectionFactory _connectionFactory;
    private readonly string _exchangeName;

    public RabbitMqEventPublisher(IConfiguration configuration, ILogger<RabbitMqEventPublisher> logger)
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
            ClientProvidedName = "product-api-event-publisher"
        };
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken)
    {
        var eventName = typeof(T).Name;
        var routingKey = ResolveRoutingKey<T>();
        var payload = JsonSerializer.Serialize(message);
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
                routingKey: routingKey,
                mandatory: false,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Published RabbitMQ event {EventName} with routing key {RoutingKey} to exchange {ExchangeName}",
                eventName,
                routingKey,
                _exchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish RabbitMQ event {EventName}. RoutingKey: {RoutingKey}. Exchange: {ExchangeName}",
                eventName,
                routingKey,
                _exchangeName);
        }
    }

    private static string ResolveRoutingKey<T>()
    {
        return typeof(T) == typeof(ProductCreatedEvent)
            ? "product.created"
            : typeof(T) == typeof(ProductUpdatedEvent)
                ? "product.updated"
                : typeof(T).Name;
    }
}
