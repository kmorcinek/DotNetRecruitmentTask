using System.Net;
using System.Net.Http.Headers;
using ProductService.IntegrationTests.Common;
using Shouldly;
using TestHelpers;

namespace ProductService.IntegrationTests;

[Collection("integration")]
public class ProductsControllerIntegrationTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string Path = "products";

    [Fact]
    public async Task create_product_given_valid_payload_should_return_created()
    {
        var userId = Guid.NewGuid();
        Authenticate(userId, "write");

        var request = new
        {
            Name = "Test Product",
            Description = "Test Description",
            Price = 99.99m
        };

        var response = await _client.PostAsJsonAsync(Path, request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var locationPath = response.Headers.Location!.PathAndQuery;
        locationPath.ShouldStartWith("/Products/");

        var productId = Guid.Parse(locationPath.Split('/').Last());
        productId.ShouldNotBe(Guid.Empty);
    }

    private void Authenticate(Guid userId, string role)
    {
        var jwt = AuthHelper.GenerateJwt(userId.ToString(), role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
    }
}
