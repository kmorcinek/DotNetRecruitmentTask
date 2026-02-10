namespace ProductService.Application.Commands;

public record CreateProductCommand(
    string Name,
    string Description,
    decimal Price
);
