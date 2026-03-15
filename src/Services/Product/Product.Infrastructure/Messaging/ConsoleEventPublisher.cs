using System.Text.Json;
using Microsoft.Extensions.Logging;
using Product.Application.Abstractions;

namespace Product.Infrastructure.Messaging;

public sealed class ConsoleEventPublisher : IEventPublisher
{
    private readonly ILogger<ConsoleEventPublisher> _logger;

    public ConsoleEventPublisher(ILogger<ConsoleEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Published domain event: {EventName} {Payload}", typeof(T).Name, JsonSerializer.Serialize(message));
        return Task.CompletedTask;
    }
}
