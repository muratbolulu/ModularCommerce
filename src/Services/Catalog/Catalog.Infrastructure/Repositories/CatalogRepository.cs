using Catalog.Application.Abstractions;
using Catalog.Domain.Entities;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Repositories;

public sealed class CatalogRepository : ICatalogRepository
{
    private readonly CatalogDbContext _dbContext;

    public CatalogRepository(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CatalogItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken)
        => await _dbContext.CatalogItems.FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken);

    public async Task UpsertAsync(CatalogItem item, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.CatalogItems.FirstOrDefaultAsync(x => x.ProductId == item.ProductId, cancellationToken);
        if (existing is null)
        {
            await _dbContext.CatalogItems.AddAsync(item, cancellationToken);
        }
        else
        {
            existing.Update(item.Name, item.Price, item.Stock);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveByProductIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.CatalogItems.FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        _dbContext.CatalogItems.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CatalogItem>> ListAsync(CancellationToken cancellationToken)
        => await _dbContext.CatalogItems.OrderByDescending(x => x.LastSyncedAtUtc).ToListAsync(cancellationToken);
}
