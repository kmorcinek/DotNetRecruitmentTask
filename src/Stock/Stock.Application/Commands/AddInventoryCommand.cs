using Abstractions.Commands;

namespace Stock.Application.Commands;

public record AddInventoryCommand(
    Guid ProductId,
    int Quantity,
    string AddedBy
) : ICommand<Guid>;
