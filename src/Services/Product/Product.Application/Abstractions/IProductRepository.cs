using Product.Domain.Entities;

namespace Product.Application.Abstractions;

public interface IProductRepository
{
    Task AddAsync(ProductEntity product, CancellationToken cancellationToken);
    Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProductEntity>> GetAllAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
