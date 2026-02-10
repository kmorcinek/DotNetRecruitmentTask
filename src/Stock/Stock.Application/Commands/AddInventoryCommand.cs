namespace Stock.Application.Commands;

public record AddInventoryCommand(
    Guid ProductId,
    int Quantity
);
