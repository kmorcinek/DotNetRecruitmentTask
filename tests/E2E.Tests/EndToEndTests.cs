using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using E2E.Tests.Infrastructure;

namespace E2E.Tests;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class EndToEndTests : IClassFixture<TestInfrastructure>
{
    private readonly HttpClient _productClient = new();
    private readonly HttpClient _inventoryClient = new();

    // Use locally running services for E2E tests
    private const string ProductServiceUrl = "http://localhost:5044";
    private const string InventoryServiceUrl = "http://localhost:5132";

    public EndToEndTests(TestInfrastructure infrastructure)
    {
        var productToken = infrastructure.GenerateJwtToken(
            "ProductService",
            "ProductService",
            new[] { "read", "write" });

        var inventoryToken = infrastructure.GenerateJwtToken(
            "InventoryService",
            "InventoryService",
            new[] { "read", "write" });

        _productClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", productToken);

        _inventoryClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", inventoryToken);
    }

    [Fact]
    public async Task Should_Update_Product_Amount_When_Inventory_Added()
    {
        // Act 1: Create a product
        var createProductRequest = new
        {
            name = $"E2E Test Product {Guid.NewGuid()}",
            description = "Product created by E2E test",
            price = 99.99m
        };

        var createdProduct = await CreateProduct(createProductRequest);

        // Initial amount should be 0
        Assert.Equal(0, createdProduct.Amount);

        // Wait for ProductCreatedEvent to be consumed by InventoryService
        await Task.Delay(2000);

        // Act 2: Add inventory for the product
        var addInventoryRequest = new
        {
            productId = createdProduct.Id,
            quantity = 25
        };

        await AddInventory(addInventoryRequest);

        // Act 3: Wait for event processing (increased for OpenTelemetry overhead)
        await Task.Delay(4000);

        // Assert: Verify product amount was updated
        var updatedProduct = await GetProduct(createdProduct.Id);
        Assert.Equal(25, updatedProduct.Amount);
    }

    [Fact]
    public async Task Should_Accumulate_Inventory_Amounts()
    {
        // Create a product
        var createProductRequest = new
        {
            name = $"E2E Accumulation Test {Guid.NewGuid()}",
            description = "Testing inventory accumulation",
            price = 149.99m
        };

        var createdProduct = await CreateProduct(createProductRequest);

        // Wait for ProductCreatedEvent to be consumed by InventoryService (increased for OpenTelemetry overhead)
        await Task.Delay(3000);

        // Add inventory multiple times
        await AddInventory(new { productId = createdProduct.Id, quantity = 10 });
        await AddInventory(new { productId = createdProduct.Id, quantity = 15 });

        // Wait for events to process (increased for OpenTelemetry overhead)
        await Task.Delay(5000);

        // Verify total amount
        var updatedProduct = await GetProduct(createdProduct.Id);
        Assert.Equal(25, updatedProduct.Amount); // 10 + 15 = 25
    }

    [Fact]
    public async Task Should_Validate_Product_Exists_Before_Adding_Inventory()
    {
        // Act - Try to add inventory for non-existent product
        var addInventoryRequest = new
        {
            productId = Guid.NewGuid(), // Random non-existent ID
            quantity = 10
        };

        var response = await AddInventoryRaw(addInventoryRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<ProductDto> CreateProduct(object request)
    {
        var response = await _productClient.PostAsJsonAsync($"{ProductServiceUrl}/products", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(product);
        return product;
    }

    private async Task AddInventory(object request)
    {
        var response = await _inventoryClient.PostAsJsonAsync($"{InventoryServiceUrl}/inventory", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> AddInventoryRaw(object request)
    {
        return await _inventoryClient.PostAsJsonAsync($"{InventoryServiceUrl}/inventory", request);
    }

    private async Task<ProductDto> GetProduct(Guid productId)
    {
        var response = await _productClient.GetAsync($"{ProductServiceUrl}/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(product);
        return product;
    }

    private class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
