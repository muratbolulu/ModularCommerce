using Catalog.Domain.Entities;

namespace Catalog.Api.Features.Catalog.Shared;

public sealed record CatalogSagaView(string SagaKey, Guid ProductId, string EventType, CatalogSagaState State, string? ErrorMessage, DateTime UpdatedAtUtc);
