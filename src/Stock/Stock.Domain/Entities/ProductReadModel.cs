namespace Stock.Domain.Entities;

public class ProductReadModel
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; }
}
