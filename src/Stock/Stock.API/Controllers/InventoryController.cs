using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stock.API.Middleware;
using Stock.Application.Commands;

namespace Stock.API.Controllers;

[ApiController]
[Route("[controller]")]
public class InventoryController(Application.Services.AddInventoryHandler addInventoryHandler, ILogger<InventoryController> logger)
    : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "write")]
    public async Task<IActionResult> AddInventory([FromBody] AddInventoryCommand command, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        logger.LogInformation("Adding inventory for product {ProductId}, quantity {Quantity}",
            command.ProductId, command.Quantity);

        // As next step it should be simple in-memory Command/Query Dispatcher
        await addInventoryHandler.Handle(command, userId, cancellationToken);

        return Ok();
    }
}
