using System.Diagnostics;
using Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;
using ProductService.Infrastructure.IdempotencyTracking;

namespace ProductService.Application.Consumers;

public class ProductInventoryAddedHandler(
    IProductRepository productRepository,
    ProductDbContext dbContext,
    ILogger<ProductInventoryAddedHandler> logger)
{
    private static readonly ActivitySource ActivitySource = new("ProductService.Handlers");
    public async Task Handle(ProductInventoryAddedEvent message, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("Handle ProductInventoryAddedEvent", ActivityKind.Consumer);
        activity?.SetTag("messaging.event_id", message.EventId);
        activity?.SetTag("messaging.product_id", message.ProductId);
        activity?.SetTag("messaging.quantity", message.Quantity);

        logger.LogInformation(
            "Received ProductInventoryAddedEvent: Event={@Event}",
            message);

        using (var idempotencyCheck = ActivitySource.StartActivity("Idempotency Check"))
        {
            var alreadyProcessed = await dbContext.ProcessedEvents
                .AnyAsync(e => e.EventId == message.EventId, cancellationToken);

            idempotencyCheck?.SetTag("already_processed", alreadyProcessed);

            if (alreadyProcessed)
            {
                logger.LogInformation(
                    "Event {EventId} already processed. Skipping.", message.EventId);
                return;
            }
        }

        using (var updateSpan = ActivitySource.StartActivity("Update Product Amount"))
        {
            var product = await productRepository.GetById(message.ProductId, cancellationToken);
            if (product == null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, $"Product {message.ProductId} not found");
                logger.LogWarning(
                    "Product {ProductId} not found for event {EventId}",
                    message.ProductId, message.EventId);
                throw new InvalidOperationException($"Product {message.ProductId} not found");
            }

            updateSpan?.SetTag("product.old_amount", product.Amount);
            product.IncrementStockAmount(message.Quantity);
            updateSpan?.SetTag("product.new_amount", product.Amount);

            dbContext.ProcessedEvents.Add(new ProcessedEvent
            {
                EventId = message.EventId,
                ProcessedAt = DateTime.UtcNow
            });

            await productRepository.Update(product, cancellationToken);
            
            activity?.SetStatus(ActivityStatusCode.Ok);
            logger.LogInformation(
                "Successfully processed event {EventId}. Updated product {ProductId} amount to {Amount}",
                message.EventId, message.ProductId, product.Amount);
        }
    }
}
