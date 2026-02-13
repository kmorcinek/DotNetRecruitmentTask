using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wolverine;

namespace ProductService.IntegrationTests.Common;

public class TestApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _dbContainer;

    public async ValueTask InitializeAsync()
    {
        _dbContainer = new PostgreSqlBuilder("postgres:18.1-alpine")
            .WithDatabase("productdb-test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _dbContainer.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("test");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _dbContainer!.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            services.DisableAllExternalWolverineTransports();
        });
    }

    public new async ValueTask DisposeAsync()
    {
        if (_dbContainer != null)
            await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}

