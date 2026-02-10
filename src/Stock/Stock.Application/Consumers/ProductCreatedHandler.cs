using System.Diagnostics;
using Contracts.Events;
using Microsoft.Extensions.Logging;
using Stock.Domain.Entities;
using Stock.Domain.Repositories;

namespace Stock.Application.Consumers;

public class ProductCreatedHandler(
    IProductReadModelRepository productReadModelRepository,
    ILogger<ProductCreatedHandler> logger)
{
    private static readonly ActivitySource ActivitySource = new("StockService.Handlers");
    public async Task Handle(ProductCreatedEvent message, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("Handle ProductCreatedEvent", ActivityKind.Consumer);
        activity?.SetTag("messaging.event_id", message.EventId);
        activity?.SetTag("messaging.product_id", message.ProductId);
        activity?.SetTag("messaging.product_name", message.Name);

        logger.LogInformation(
            "Received ProductCreatedEvent: Event={@Event}",
            message);

        using (var idempotencyCheck = ActivitySource.StartActivity("Idempotency Check"))
        {
            var exists = await productReadModelRepository.Exists(message.ProductId, cancellationToken);
            idempotencyCheck?.SetTag("already_exists", exists);

            if (exists)
            {
                logger.LogInformation(
                    "Product {ProductId} already exists in read model. Skipping.", message.ProductId);
                return;
            }
        }

        using (var syncSpan = ActivitySource.StartActivity("Sync Product to Read Model"))
        {
            var productReadModel = new ProductReadModel
            {
                ProductId = message.ProductId,
                Name = message.Name,
                SyncedAt = DateTime.UtcNow
            };

            await productReadModelRepository.Insert(productReadModel, cancellationToken);
            syncSpan?.SetTag("product.synced_at", productReadModel.SyncedAt);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        logger.LogInformation(
            "Successfully synced product {ProductId} to read model", message.ProductId);
    }
}
