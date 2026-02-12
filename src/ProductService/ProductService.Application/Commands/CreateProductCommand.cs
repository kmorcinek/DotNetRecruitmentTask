using Abstractions.Commands;

namespace ProductService.Application.Commands;

public record CreateProductCommand(
    string Name,
    string Description,
    decimal Price
) : ICommand<Guid>;
