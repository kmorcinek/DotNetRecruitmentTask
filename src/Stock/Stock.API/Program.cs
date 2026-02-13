using System.Text;
using Infrastructure.ErrorHandling;
using Wolverine;
using Wolverine.RabbitMQ;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Npgsql;
using Serilog;
using Serilog.Enrichers.Span;
using Stock.Application.Consumers;
using Stock.Application.Services;
using Stock.Application.Validators;
using Stock.Domain;
using Stock.Domain.Repositories;
using Stock.Infrastructure.Data;
using Stock.Infrastructure.Repositories;

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
var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "StockService";
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
        .AddSource("StockService.Handlers")  // Custom business spans
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
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "InventoryService";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "InventoryService";

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
    ?? "Host=localhost;Port=5433;Database=inventorydb;Username=postgres;Password=postgres";

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register repositories and services
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IProductReadModelRepository, ProductReadModelRepository>();
builder.Services.AddScoped<IProductChecker, ProductReadModelRepository>();
builder.Services.AddScoped<AddInventoryHandler>();
builder.Services.AddScoped<AddInventoryCommandValidator>();

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

    // Publish to product-inventory-added queue
    opts.PublishMessage<Contracts.Events.ProductInventoryAddedEvent>()
        .ToRabbitQueue("product-inventory-added");

    // Listen for ProductCreatedEvent
    opts.ListenToRabbitQueue("product-created");

    // Discover handlers in Application assembly
    opts.Discovery.IncludeAssembly(typeof(ProductCreatedHandler).Assembly);
});

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
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
