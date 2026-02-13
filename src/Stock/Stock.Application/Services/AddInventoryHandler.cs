using Abstractions.Commands;
using Contracts.Events;
using Microsoft.Extensions.Logging;
using Stock.Application.Commands;
using Stock.Application.Validators;
using Stock.Domain.Entities;
using Stock.Domain.Repositories;
using Wolverine;

namespace Stock.Application.Services;

public class AddInventoryHandler(
    IInventoryRepository repository,
    IMessageBus messageBus,
    AddInventoryCommandValidator validator,
    ILogger<AddInventoryHandler> logger)
    : ICommandHandler<AddInventoryCommand, Guid>
{
    public async Task<Guid> Handle(AddInventoryCommand command, CancellationToken cancellationToken)
    {
        await validator.Validate(command, cancellationToken);

        var inventory = Inventory.Create(command.ProductId, command.Quantity, command.AddedBy);

        await repository.Insert(inventory, cancellationToken);

        var inventoryEvent = new ProductInventoryAddedEvent(
            Guid.NewGuid(),
            command.ProductId,
            command.Quantity,
            DateTime.UtcNow
        );

        await messageBus.PublishAsync(inventoryEvent);

        logger.LogInformation("Published ProductInventoryAddedEvent: Event={@Event}", inventoryEvent);

        return inventory.Id;
    }
}