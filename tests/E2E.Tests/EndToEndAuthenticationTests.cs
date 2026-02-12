using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace E2E.Tests;

[SuppressMessage("Usage",
    "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class EndToEndAuthenticationTests
{
    private readonly HttpClient _productClient = new();

    private const string ProductServiceUrl = "http://localhost:5044";

    [Fact]
    public async Task Should_Require_Authentication_For_Protected_Endpoints()
    {
        // Attempt to create product without token
        var createProductRequest = new
        {
            name = "Unauthorized Product",
            description = "Should fail",
            price = 50.00m
        };

        var response = await _productClient.PostAsJsonAsync<object>($"{ProductServiceUrl}/products", createProductRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
