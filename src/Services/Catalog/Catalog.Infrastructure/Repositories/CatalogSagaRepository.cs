using Catalog.Application.Abstractions;
using Catalog.Domain.Entities;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Repositories;

public sealed class CatalogSagaRepository : ICatalogSagaRepository
{
    private readonly CatalogDbContext _dbContext;

    public CatalogSagaRepository(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CatalogSagaInstance?> GetBySagaKeyAsync(string sagaKey, CancellationToken cancellationToken)
        => await _dbContext.CatalogSagas.FirstOrDefaultAsync(x => x.SagaKey == sagaKey, cancellationToken);

    public async Task AddAsync(CatalogSagaInstance saga, CancellationToken cancellationToken)
    {
        await _dbContext.CatalogSagas.AddAsync(saga, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CatalogSagaInstance saga, CancellationToken cancellationToken)
    {
        _dbContext.CatalogSagas.Update(saga);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CatalogSagaInstance>> ListAsync(CancellationToken cancellationToken)
        => await _dbContext.CatalogSagas.OrderByDescending(x => x.UpdatedAtUtc).ToListAsync(cancellationToken);
}
