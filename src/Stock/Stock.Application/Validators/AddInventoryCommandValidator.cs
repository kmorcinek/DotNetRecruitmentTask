using Abstractions.Exceptions;
using Stock.Application.Commands;
using Stock.Domain;

namespace Stock.Application.Validators;

public class AddInventoryCommandValidator(IProductChecker productChecker)
{
    public async Task Validate(AddInventoryCommand command, CancellationToken cancellationToken)
    {
        if (command.Quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than 0");
        }

        if (!await productChecker.Exists(command.ProductId, cancellationToken))
        {
            throw new DomainException($"Product {command.ProductId} not found");
        }
    }
}