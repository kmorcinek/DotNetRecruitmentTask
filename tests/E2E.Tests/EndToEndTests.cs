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
        // Arrange
        var createProductRequest = new
        {
            name = $"E2E Test Product {Guid.NewGuid()}",
            description = "Product created by E2E test",
            price = 99.99m
        };

        var createdProduct = await CreateProduct(createProductRequest);

        // Initial amount should be 0
        Assert.Equal(0, createdProduct.Amount);

        await WaitForProductProcessing();

        // Act
        await AddInventory(createdProduct.Id, 25);

        // Assert
        var updatedProduct = await WaitForConditionAsync(
            () => GetProduct(createdProduct.Id),
            p => p.Amount == 25);

        Assert.Equal(25, updatedProduct.Amount);
    }

    [Fact]
    public async Task Should_Accumulate_Inventory_Amounts()
    {
        // Arrange
        var createProductRequest = new
        {
            name = $"E2E Accumulation Test {Guid.NewGuid()}",
            description = "Testing inventory accumulation",
            price = 149.99m
        };

        var createdProduct = await CreateProduct(createProductRequest);

        await WaitForProductProcessing();

        // Act
        await AddInventory(createdProduct.Id, 10);
        await AddInventory(createdProduct.Id, 15);

        // Assert
        var updatedProduct = await WaitForConditionAsync(
            () => GetProduct(createdProduct.Id),
            p => p.Amount == 25); // 10 + 15 = 25

        Assert.Equal(25, updatedProduct.Amount);
    }

    [Fact]
    public async Task Should_Validate_Product_Exists_Before_Adding_Inventory()
    {
        // Act - Try to add inventory for non-existent product
        var nonExistentProductId = Guid.NewGuid();
        var response = await AddInventoryRaw(nonExistentProductId, 10);

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

    private async Task AddInventory(Guid productId, int quantity)
    {
        var request = new { productId, quantity };
        var response = await _inventoryClient.PostAsJsonAsync($"{InventoryServiceUrl}/inventory", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> AddInventoryRaw(Guid productId, int quantity)
    {
        var request = new { productId, quantity };
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

    private static async Task WaitForProductProcessing()
    {
        // Increased temporary for CI
        await Task.Delay(3000);
    }

    private static async Task<T> WaitForConditionAsync<T>(
        Func<Task<T>> getValue,
        Func<T, bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollingInterval = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var interval = pollingInterval ?? TimeSpan.FromMilliseconds(500);
        var endTime = DateTime.UtcNow.Add(actualTimeout);

        while (DateTime.UtcNow < endTime)
        {
            var value = await getValue();
            if (condition(value))
            {
                return value;
            }

            await Task.Delay(interval);
        }

        var lastValue = await getValue();
        if (condition(lastValue))
        {
            return lastValue;
        }

        throw new TimeoutException($"Condition not met within {actualTimeout.TotalSeconds}s");
    }

    private class ProductDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public int Amount { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
