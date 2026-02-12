using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using E2E.Tests.Infrastructure;

namespace E2E.Tests;

[SuppressMessage("Usage",
    "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class EndToEndAuthorizationTests(TestInfrastructure infrastructure) : IClassFixture<TestInfrastructure>
{
    private readonly HttpClient _productClient = new();

    private const string ProductServiceUrl = "http://localhost:5044";

    [Fact]
    public async Task Should_Forbid_Create_Product_With_Read_Only_Token()
    {
        // Arrange - Generate token with only "read" permission
        var readOnlyToken = infrastructure.GenerateJwtToken(
            "ReadOnlyUser",
            "ReadOnlyUser",
            new[] { "read" });

        _productClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", readOnlyToken);

        var createProductRequest = new
        {
            name = "Unauthorized Product",
            description = "Should be forbidden",
            price = 50.00m
        };

        // Act
        var response = await _productClient.PostAsJsonAsync<object>(
            $"{ProductServiceUrl}/products",
            createProductRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Should_Forbid_Read_Products_With_Write_Only_Token()
    {
        // Arrange - Generate token with only "write" permission
        var writeOnlyToken = infrastructure.GenerateJwtToken(
            "WriteOnlyUser",
            "WriteOnlyUser",
            new[] { "write" });

        _productClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", writeOnlyToken);

        // Act - Attempt to read products list with write-only token
        var response = await _productClient.GetAsync($"{ProductServiceUrl}/products");

        // Assert - Service returns 401 Unauthorized for insufficient role permissions
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
