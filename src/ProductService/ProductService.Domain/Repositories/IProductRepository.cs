using ProductService.Domain.Entities;

namespace ProductService.Domain.Repositories;

public interface IProductRepository
{
    Task<Product> Insert(Product product, CancellationToken cancellationToken);
    Task<Product?> GetById(Guid id, CancellationToken cancellationToken);
    Task<List<Product>> GetAll(CancellationToken cancellationToken);
    Task Update(Product product, CancellationToken cancellationToken);
}
