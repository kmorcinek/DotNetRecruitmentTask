using Abstractions.Commands;
using Contracts.Events;
using ProductService.Application.Commands;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using Wolverine;

namespace ProductService.Application.Services;

public class CreateProductHandler(IProductRepository repository, IMessageBus messageBus)
    : ICommandHandler<CreateProductCommand, Guid>
{
    public async Task<Guid> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Product name is required", nameof(command.Name));

        if (command.Price <= 0)
            throw new ArgumentException("Product price must be greater than 0", nameof(command.Price));

        var product = Product.Create(command.Name,command.Description, command.Price);

        var createdProduct = await repository.Insert(product, cancellationToken);

        // Publish ProductCreatedEvent
        var productCreatedEvent = new ProductCreatedEvent(
            EventId: Guid.NewGuid(),
            ProductId: createdProduct.Id,
            Name: createdProduct.Name,
            OccurredAt: DateTime.UtcNow
        );

        await messageBus.PublishAsync(productCreatedEvent);

        return createdProduct.Id;
    }

    public async Task<List<Product>> GetAllProducts(CancellationToken cancellationToken)
    {
        return await repository.GetAll(cancellationToken);
    }

    public async Task<Product?> GetProductById(Guid id, CancellationToken cancellationToken)
    {
        return await repository.GetById(id, cancellationToken);
    }
}
