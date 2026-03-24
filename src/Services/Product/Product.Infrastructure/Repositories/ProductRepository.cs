using Microsoft.EntityFrameworkCore;
using Product.Application.Abstractions;
using Product.Domain.Entities;
using Product.Infrastructure.Persistence;

namespace Product.Infrastructure.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly ProductDbContext _dbContext;

    public ProductRepository(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ProductEntity product, CancellationToken cancellationToken)
        => await _dbContext.Products.AddAsync(product, cancellationToken);

    public async Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await _dbContext.Products.FirstOrDefaultAsync(x => x.Id == id && x.IsActive && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyCollection<ProductEntity>> GetAllAsync(CancellationToken cancellationToken)
        => await _dbContext.Products
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.SoftDelete();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
        => await _dbContext.SaveChangesAsync(cancellationToken);
}
