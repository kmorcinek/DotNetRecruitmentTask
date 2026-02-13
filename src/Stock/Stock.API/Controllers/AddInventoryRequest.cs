namespace Stock.API.Controllers;

public record AddInventoryRequest(
    Guid ProductId,
    int Quantity
);
