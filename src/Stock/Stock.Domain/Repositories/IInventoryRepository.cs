using Stock.Domain.Entities;

namespace Stock.Domain.Repositories;

public interface IInventoryRepository
{
    Task<Inventory> Insert(Inventory inventory, CancellationToken cancellationToken);
}
