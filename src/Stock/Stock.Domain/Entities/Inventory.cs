namespace Stock.Domain.Entities;

public class Inventory
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public DateTime AddedAt { get; private set; }
    public string AddedBy { get; private set; } = string.Empty;

    public static Inventory Create(Guid productId, int quantity, string addedBy)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
        }

        if (string.IsNullOrWhiteSpace(addedBy))
        {
            throw new ArgumentException("AddedBy must not be empty", nameof(addedBy));
        }

        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = quantity,
            AddedAt = DateTime.UtcNow,
            AddedBy = addedBy
        };
        
        return inventory;
    }
}
