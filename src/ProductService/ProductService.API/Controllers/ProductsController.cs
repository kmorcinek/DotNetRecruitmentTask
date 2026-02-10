using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.Commands;

namespace ProductService.API.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductsController(Application.Services.CreateProductHandler createProductHandler, ILogger<ProductsController> logger)
    : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "write")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating product: {Name}", command.Name);

        // As next step it should be simple in-memory Command/Query Dispatcher
        var product = await createProductHandler.Handle(command, cancellationToken);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpGet]
    [Authorize(Roles = "read")]
    public async Task<IActionResult> GetProducts(CancellationToken cancellationToken)
    {
        var products = await createProductHandler.GetAllProducts(cancellationToken);
        return Ok(products);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProduct(Guid id, CancellationToken cancellationToken)
    {
        var product = await createProductHandler.GetProductById(id, cancellationToken);
        if (product == null)
            return NotFound();
        return Ok(product);
    }
}
