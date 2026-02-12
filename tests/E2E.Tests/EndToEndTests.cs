using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using E2E.Tests.Infrastructure;

namespace E2E.Tests;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class EndToEndTests(TestInfrastructure infrastructure) : IClassFixture<TestInfrastructure>
{
    private readonly HttpClient _httpClient = new();

    // Use locally running services for E2E tests
    private const string ProductServiceUrl = "http://localhost:5044";
    private const string InventoryServiceUrl = "http://localhost:5132";

    [Fact]
    public async Task Should_Update_Product_Amount_When_Inventory_Added()
    {
        // Arrange - Generate JWT tokens
        var productToken = infrastructure.GenerateJwtToken(
            "ProductService",
            "ProductService",
            new[] { "read", "write" });

        var inventoryToken = infrastructure.GenerateJwtToken(
            "InventoryService",
            "InventoryService",
            new[] { "read", "write" });

        // Act 1: Create a product
        var createProductRequest = new
        {
            name = $"E2E Test Product {Guid.NewGuid()}",
            description = "Product created by E2E test",
            price = 99.99m
        };

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", productToken);

        var createProductResponse = await _httpClient.PostAsJsonAsync(
            $"{ProductServiceUrl}/products",
            createProductRequest);

        Assert.Equal(HttpStatusCode.Created, createProductResponse.StatusCode);

        var createdProduct = await createProductResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(createdProduct);
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

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", inventoryToken);

        var addInventoryResponse = await _httpClient.PostAsJsonAsync(
            $"{InventoryServiceUrl}/inventory",
            addInventoryRequest);

        Assert.Equal(HttpStatusCode.OK, addInventoryResponse.StatusCode);

        // Act 3: Wait for event processing (increased for OpenTelemetry overhead)
        await Task.Delay(4000);

        // Assert: Verify product amount was updated
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", productToken);

        var getProductResponse = await _httpClient.GetAsync(
            $"{ProductServiceUrl}/products/{createdProduct.Id}");

        Assert.Equal(HttpStatusCode.OK, getProductResponse.StatusCode);

        var updatedProduct = await getProductResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(updatedProduct);
        Assert.Equal(25, updatedProduct.Amount);
    }

    [Fact]
    public async Task Should_Accumulate_Inventory_Amounts()
    {
        // Arrange - Generate JWT tokens
        var productToken = infrastructure.GenerateJwtToken(
            "ProductService",
            "ProductService",
            new[] { "read", "write" });

        var inventoryToken = infrastructure.GenerateJwtToken(
            "InventoryService",
            "InventoryService",
            new[] { "read", "write" });

        // Create a product
        var createProductRequest = new
        {
            name = $"E2E Accumulation Test {Guid.NewGuid()}",
            description = "Testing inventory accumulation",
            price = 149.99m
        };

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", productToken);

        var createProductResponse = await _httpClient.PostAsJsonAsync(
            $"{ProductServiceUrl}/products",
            createProductRequest);

        var createdProduct = await createProductResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(createdProduct);

        // Wait for ProductCreatedEvent to be consumed by InventoryService (increased for OpenTelemetry overhead)
        await Task.Delay(3000);

        // Add inventory multiple times
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", inventoryToken);

        await _httpClient.PostAsJsonAsync(
            $"{InventoryServiceUrl}/inventory",
            new { productId = createdProduct.Id, quantity = 10 });

        await _httpClient.PostAsJsonAsync(
            $"{InventoryServiceUrl}/inventory",
            new { productId = createdProduct.Id, quantity = 15 });

        await _httpClient.PostAsJsonAsync(
            $"{InventoryServiceUrl}/inventory",
            new { productId = createdProduct.Id, quantity = 5 });

        // Wait for events to process (increased for OpenTelemetry overhead)
        await Task.Delay(5000);

        // Verify total amount
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", productToken);

        var getProductResponse = await _httpClient.GetAsync(
            $"{ProductServiceUrl}/products/{createdProduct.Id}");

        var updatedProduct = await getProductResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(updatedProduct);
        Assert.Equal(30, updatedProduct.Amount); // 10 + 15 + 5 = 30
    }

    [Fact]
    public async Task Should_Validate_Product_Exists_Before_Adding_Inventory()
    {
        // Arrange
        var inventoryToken = infrastructure.GenerateJwtToken(
            "InventoryService",
            "InventoryService",
            new[] { "read", "write" });

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", inventoryToken);

        // Act - Try to add inventory for non-existent product
        var addInventoryRequest = new
        {
            productId = Guid.NewGuid(), // Random non-existent ID
            quantity = 10
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{InventoryServiceUrl}/inventory",
            addInventoryRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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