namespace ProductService.Domain.Entities;

public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Product Create(string name, string description, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name must not be empty", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Product description must not be empty", nameof(description));
        }

        if (price <= 0)
        {
            throw new ArgumentException("Product price must be greater than 0", nameof(price));
        }
        
        var now = DateTime.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Price = price,
            Amount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        
        return product;
    }
    
    public void IncrementStockAmount(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than 0", nameof(amount));
        }
        
        Amount += amount;
        UpdatedAt = DateTime.UtcNow;
    }
}