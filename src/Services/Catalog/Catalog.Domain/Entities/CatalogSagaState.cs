namespace Catalog.Domain.Entities;

public enum CatalogSagaState
{
    Started = 1,
    CatalogWriteCompleted = 2,
    Completed = 3,
    Compensating = 4,
    Compensated = 5,
    Failed = 6
}
