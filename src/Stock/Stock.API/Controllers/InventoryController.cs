using Abstractions.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stock.API.Middleware;
using Stock.Application.Commands;

namespace Stock.API.Controllers;

[ApiController]
[Route("[controller]")]
public class InventoryController(ICommandDispatcher commandDispatcher, ILogger<InventoryController> logger)
    : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "write")]
    public async Task<IActionResult> AddInventory([FromBody] AddInventoryRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        logger.LogInformation("Adding inventory for product {ProductId}, quantity {Quantity}",
            request.ProductId, request.Quantity);

        var command = new AddInventoryCommand(request.ProductId, request.Quantity, userId);
        await commandDispatcher.Dispatch<AddInventoryCommand, Guid>(command, cancellationToken);

        return Ok();
    }
}
