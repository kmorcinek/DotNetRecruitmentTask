using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;

namespace ProductService.Infrastructure.Repositories;

public class ProductRepository(ProductDbContext context) : IProductRepository
{
    public async Task<Product> Insert(Product product, CancellationToken cancellationToken)
    {
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task<Product?> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await context.Products.FindAsync([id], cancellationToken);
    }

    public async Task<List<Product>> GetAll(CancellationToken cancellationToken)
    {
        return await context.Products.ToListAsync(cancellationToken);
    }

    public async Task Update(Product product, CancellationToken cancellationToken)
    {
        context.Products.Update(product);
        await context.SaveChangesAsync(cancellationToken);
    }
}
