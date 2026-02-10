namespace Contracts.Events;

public record ProductCreatedEvent(
    Guid EventId,
    Guid ProductId,
    string Name,
    DateTime OccurredAt
);
