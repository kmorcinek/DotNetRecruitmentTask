using Microsoft.EntityFrameworkCore;
using Stock.Domain;
using Stock.Domain.Entities;
using Stock.Domain.Repositories;
using Stock.Infrastructure.Data;

namespace Stock.Infrastructure.Repositories;

public class ProductReadModelRepository(InventoryDbContext context) : IProductReadModelRepository, IProductChecker
{
    public async Task Insert(ProductReadModel product, CancellationToken cancellationToken)
    {
        context.ProductReadModels.Add(product);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Exists(Guid productId, CancellationToken cancellationToken)
    {
        return await context.ProductReadModels
            .AnyAsync(p => p.ProductId == productId, cancellationToken);
    }
}
