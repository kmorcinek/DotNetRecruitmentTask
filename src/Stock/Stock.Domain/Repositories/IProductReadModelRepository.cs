using Stock.Domain.Entities;

namespace Stock.Domain.Repositories;

public interface IProductReadModelRepository
{
    Task Insert(ProductReadModel product, CancellationToken cancellationToken);
    Task<bool> Exists(Guid productId, CancellationToken cancellationToken);
}
