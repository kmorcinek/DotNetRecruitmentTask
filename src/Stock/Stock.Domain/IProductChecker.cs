namespace Stock.Domain;

public interface IProductChecker
{
    Task<bool> Exists(Guid productId, CancellationToken cancellationToken);
}