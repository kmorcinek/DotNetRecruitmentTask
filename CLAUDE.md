# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

.NET 10 distributed system demonstrating event-driven microservices architecture. Two services (ProductService and StockService) communicate via RabbitMQ with idempotent event processing.

## Common Commands

### Build and Test
```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/E2E.Tests/E2E.Tests.csproj

# Run tests with no build
dotnet test --no-build
```

### Running Services

**With Docker Compose (recommended):**
```bash
# Start all services
docker-compose up --build

# Start only infrastructure (for local service development)
docker compose up -d postgres-product postgres-inventory rabbitmq
```

**Locally (for development):**
```bash
# ProductService (port 5044)
cd src/ProductService/ProductService.API
dotnet run

# StockService (port 5132) - in separate terminal
cd src/Stock/Stock.API
dotnet run
```

**Docker Compose vs Local Ports:**
- Docker Compose: ProductService=5001, StockService=5002
- Local development: ProductService=5044, StockService=5132

### Database Migrations
```bash
# ProductService
cd src/ProductService/ProductService.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../ProductService.API

# StockService
cd src/Stock/Stock.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../Stock.API
```

Migrations apply automatically on service startup.

## Architecture

### Event-Driven Flow

**Product Creation Flow:**
1. **ProductService creates product**
   - Product created via POST /products
   - Publishes `ProductCreatedEvent` to RabbitMQ

2. **StockService consumes ProductCreatedEvent**
   - Checks if product already in local read model (idempotency)
   - Inserts product into `ProductReadModels` table
   - Maintains local cache of products for validation

**Inventory Addition Flow:**
1. **StockService receives inventory addition request**
   - Validates product exists using local `ProductReadModels` table (via `IProductChecker`)
   - Creates inventory record in local DB
   - Publishes `ProductInventoryAddedEvent` to RabbitMQ (via Wolverine)

2. **ProductService consumes ProductInventoryAddedEvent**
   - Checks idempotency: queries `ProcessedEvents` table for `EventId`
   - If already processed, skips (prevents double-increment)
   - Updates product amount
   - Inserts `ProcessedEvent` record
   - Both operations in same transaction

### Key Components

**ProductService.Application.Consumers.ProductInventoryAddedHandler**
- Implements idempotent event processing
- Uses `ProcessedEvents` table to track event IDs
- Updates product amount atomically with event tracking

**StockService.Application.Consumers.ProductCreatedHandler**
- Consumes `ProductCreatedEvent` from ProductService
- Populates local `ProductReadModels` table with product information
- Implements idempotency check to prevent duplicate inserts

**StockService.Infrastructure.Repositories.ProductReadModelRepository**
- Implements `IProductChecker` for product validation
- Maintains local read model of products in StockService database
- Used by validator to check product existence without calling ProductService

**StockService.Application.Services.StockServiceImpl**
- Publishes `ProductInventoryAddedEvent` via Wolverine `IMessageBus`
- Each event has unique `EventId` (Guid)

**Contracts/Events**
- `ProductCreatedEvent`: Published by ProductService, consumed by StockService
- `ProductInventoryAddedEvent`: Published by StockService, consumed by ProductService
- Located in `src/Shared/Contracts/`

### Messaging (Wolverine)

Wolverine handles RabbitMQ integration. Configuration in `Program.cs`:
```csharp
builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMq(rabbitConfig.Host, rabbitConfig.Username, rabbitConfig.Password);
    opts.PublishMessage<ProductInventoryAddedEvent>().ToRabbitExchange("product-inventory");
    opts.ListenToRabbitQueue("product-inventory-queue").Named("product-inventory");
});
```

### Idempotency Pattern

**Critical:** ProductService tracks `ProcessedEvents` with `EventId` to prevent duplicate processing. Always use this pattern when adding new event handlers:
1. Check if `EventId` exists in `ProcessedEvents`
2. If exists, skip processing
3. Process business logic
4. Insert `ProcessedEvent` record in same transaction

### Database Per Service

- **ProductService:** postgres-product (port 5434), database `productdb`
- **StockService:** postgres-inventory (port 5433), database `inventorydb`

Each service owns its data. No cross-database queries.

### Authentication

JWT-based with shared secret: `your-super-secret-key-min-32-chars-long-for-security`

**Roles:**
- `read`: GET endpoints
- `write`: POST/PUT/DELETE endpoints

**Generate tokens:**
- Use `GenerateTokens.csx` script: `dotnet script GenerateTokens.csx` (generates tokens for both services)
- In tests: `TestInfrastructure.GenerateJwtToken()`
- See README for jwt.io method

## Testing

### E2E Tests (TestContainers)

Located in `tests/E2E.Tests/`. Uses TestContainers to spin up PostgreSQL and RabbitMQ.

**TestInfrastructure:**
- Implements `IAsyncLifetime` (xunit v3 uses `ValueTask`, not `Task`)
- Starts 3 containers: 2x PostgreSQL, 1x RabbitMQ
- Tests expect services running locally on ports 5044 and 5132

**Running E2E tests:**
```bash
# Start services locally first
docker compose up -d postgres-product postgres-inventory rabbitmq
cd src/ProductService/ProductService.API && dotnet run &
cd src/Stock/Stock.API && dotnet run &

# Run E2E tests
dotnet test tests/E2E.Tests/E2E.Tests.csproj
```

### TestContainers v4 Changes

When using Testcontainers 4.x, constructors require image parameter:
```csharp
// Old (v3): new PostgreSqlBuilder()
// New (v4): pass image in WithImage() chained call or use new constructor
_container = new PostgreSqlBuilder()
    .WithImage("postgres:18.1-alpine")
    .Build();
```

### xUnit v3 Breaking Changes

- `IAsyncLifetime.InitializeAsync()` returns `ValueTask` (not `Task`)
- `IAsyncLifetime.DisposeAsync()` returns `ValueTask` (not `Task`)
- Package name changed: `xunit` → `xunit.v3`

## Service-to-Service Communication

**Fully Asynchronous (Event-Driven):**
- ProductService publishes `ProductCreatedEvent` when products are created
- StockService consumes `ProductCreatedEvent` and maintains local `ProductReadModels` table
- StockService validates product existence using local read model (no HTTP calls to ProductService)
- StockService publishes `ProductInventoryAddedEvent` when inventory is added
- ProductService consumes `ProductInventoryAddedEvent` and updates product amounts

**Benefits:**
- No synchronous HTTP dependencies between services
- Each service maintains its own data for queries
- Improved resilience and performance
- Services can operate independently

## Configuration

Services configured via environment variables (see `docker-compose.yml`):
- `ConnectionStrings__DefaultConnection`
- `RabbitMQ__Host`, `RabbitMQ__Username`, `RabbitMQ__Password`
- `Jwt__Secret`, `Jwt__Issuer`, `Jwt__Audience`
- `ProductService__Url` (StockService only)

## Clean Architecture Layers

Each service follows:
- **API:** Controllers, Program.cs, Swagger config
- **Application:** Business logic, handlers, validators, DTOs
- **Domain:** Entities, repository interfaces
- **Infrastructure:** EF Core, DbContext, repository implementations

Dependencies flow inward: API → Application → Domain ← Infrastructure
