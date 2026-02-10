using Stock.Domain.Entities;
using Stock.Domain.Repositories;
using Stock.Infrastructure.Data;

namespace Stock.Infrastructure.Repositories;

public class InventoryRepository(InventoryDbContext context) : IInventoryRepository
{
    public async Task<Inventory> Insert(Inventory inventory, CancellationToken cancellationToken)
    {
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync(cancellationToken);
        return inventory;
    }
}
