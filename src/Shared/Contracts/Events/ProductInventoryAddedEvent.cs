namespace Contracts.Events;

public record ProductInventoryAddedEvent(
    Guid EventId,
    Guid ProductId,
    int Quantity,
    DateTime OccurredAt
);
