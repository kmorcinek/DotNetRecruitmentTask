using System.Text;
using Infrastructure.Commands;
using Infrastructure.ErrorHandling;
using Wolverine;
using Wolverine.RabbitMQ;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Npgsql;
using ProductService.Application.Commands;
using ProductService.Application.Services;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;
using ProductService.Infrastructure.Repositories;
using Serilog;
using Serilog.Enrichers.Span;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()  // Adds TraceId/SpanId from Activity.Current
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j} " +
        "TraceId={TraceId} SpanId={SpanId}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Configure OpenTelemetry
var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "ProductService";
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["host.name"] = Environment.MachineName
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
        })
        .AddNpgsql()
        .AddSource("Wolverine")  // Critical: Wolverine's ActivitySource
        .AddSource("ProductService.Handlers")  // Custom business spans
        .AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri(otlpEndpoint);
        }));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Enter your JWT token below (without 'Bearer' prefix).",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "your-super-secret-key-min-32-chars-long-for-security";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ProductService";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ProductService";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Host=localhost;Port=5434;Database=productdb;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register repositories and services
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// TODO: registering this way cause QueryDispatcher is not ready yet and need to inject it into ProductsController
builder.Services.AddScoped<CreateProductHandler>();

// Register Commands
builder.Services
    .AddCommands([typeof(CreateProductCommand).Assembly]);

// Configure Wolverine with RabbitMQ
builder.Host.UseWolverine(opts =>
{
    var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
    var rabbitMqUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
    var rabbitMqPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

    opts.UseRabbitMq(rabbit =>
        {
            rabbit.HostName = rabbitMqHost;
            rabbit.UserName = rabbitMqUser;
            rabbit.Password = rabbitMqPass;
        })
        .AutoProvision();

    // Publish ProductCreatedEvent
    opts.PublishMessage<Contracts.Events.ProductCreatedEvent>()
        .ToRabbitQueue("product-created");

    // Listen for ProductInventoryAddedEvent
    opts.ListenToRabbitQueue("product-inventory-added");

    // Discover handlers in Application assembly
    opts.Discovery.IncludeAssembly(typeof(ProductService.Application.Consumers.ProductInventoryAddedHandler).Assembly);
});

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    db.Database.Migrate();
}

// Configure middleware
app.UseMiddleware<ErrorHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();