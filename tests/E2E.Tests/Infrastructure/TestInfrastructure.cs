using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace E2E.Tests.Infrastructure;

public class TestInfrastructure : IAsyncLifetime
{
    private PostgreSqlContainer? _productDbContainer;
    private PostgreSqlContainer? _inventoryDbContainer;
    private RabbitMqContainer? _rabbitmqContainer;

    public string ProductDbConnectionString => _productDbContainer!.GetConnectionString();
    public string InventoryDbConnectionString => _inventoryDbContainer!.GetConnectionString();
    public string RabbitMqConnectionString => _rabbitmqContainer!.GetConnectionString();

    public int ProductServicePort { get; private set; }
    public int InventoryServicePort { get; private set; }

    public async ValueTask InitializeAsync()
    {
        // Start PostgreSQL for ProductService
        _productDbContainer = new PostgreSqlBuilder("postgres:18.1-alpine")
            .WithDatabase("productdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        // Start PostgreSQL for InventoryService
        _inventoryDbContainer = new PostgreSqlBuilder("postgres:18.1-alpine")
            .WithDatabase("inventorydb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        // Start RabbitMQ
        _rabbitmqContainer = new RabbitMqBuilder("rabbitmq:3-management-alpine")
            .Build();

        await Task.WhenAll(
            _productDbContainer.StartAsync(),
            _inventoryDbContainer.StartAsync(),
            _rabbitmqContainer.StartAsync()
        );

        // Find available ports for services
        ProductServicePort = FreeTcpPort();
        InventoryServicePort = FreeTcpPort();
    }

    public async ValueTask DisposeAsync()
    {
        if (_productDbContainer != null)
            await _productDbContainer.DisposeAsync();

        if (_inventoryDbContainer != null)
            await _inventoryDbContainer.DisposeAsync();

        if (_rabbitmqContainer != null)
            await _rabbitmqContainer.DisposeAsync();
    }

    public string GenerateJwtToken(string issuer, string audience, string[] roles)
    {
        var secret = "your-super-secret-key-min-32-chars-long-for-security";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, "e2e-test-user")
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static int FreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}