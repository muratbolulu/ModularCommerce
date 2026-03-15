namespace Shared.Contracts.Events;

public sealed record ProductCreatedEvent(Guid ProductId, string Name, decimal Price, int Stock, DateTime OccurredAtUtc);

public sealed record ProductUpdatedEvent(Guid ProductId, string Name, decimal Price, int Stock, DateTime OccurredAtUtc);
