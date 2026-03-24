namespace Catalog.Domain.Entities;

public sealed class CatalogSagaInstance
{
    public Guid Id { get; private set; }
    public string SagaKey { get; private set; } = string.Empty;
    public Guid ProductId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public CatalogSagaState State { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private CatalogSagaInstance()
    {
    }

    public CatalogSagaInstance(string sagaKey, Guid productId, string eventType)
    {
        Id = Guid.NewGuid();
        SagaKey = sagaKey;
        ProductId = productId;
        EventType = eventType;
        State = CatalogSagaState.Started;
        StartedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MoveTo(CatalogSagaState state, string? errorMessage = null)
    {
        State = state;
        ErrorMessage = errorMessage;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
