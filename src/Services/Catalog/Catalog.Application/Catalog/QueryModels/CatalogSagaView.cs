using Catalog.Domain.Entities;

namespace Catalog.Application.Catalog.QueryModels;

public sealed record CatalogSagaView(string SagaKey, Guid ProductId, string EventType, CatalogSagaState State, string? ErrorMessage, DateTime UpdatedAtUtc);
