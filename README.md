# .NET Distributed System - Recruitment Task

A distributed warehouse/product management system demonstrating event-driven architecture, microservices patterns, JWT authentication, and idempotent message processing using .NET 8.

## Architecture Overview

This solution consists of two microservices communicating via RabbitMQ:

### ProductService
- Manages product catalog (CRUD operations)
- Tracks product inventory amounts
- Consumes `ProductInventoryAddedEvent` to update product quantities
- Implements **idempotent event processing** using event ID tracking

### StockService
- Records inventory additions
- Maintains local product read model synced via `ProductCreatedEvent`
- Validates products exist using local read model (no HTTP calls)
- Publishes `ProductInventoryAddedEvent` when inventory is added
- Triggers ProductService to update product amounts

### Technology Stack
- **.NET 8** (latest LTS)
- **Wolverine** with **RabbitMQ** (message bus)
- **PostgreSQL 18.1-alpine** (separate database per service)
- **Entity Framework Core** (ORM with code-first migrations)
- **JWT** (authentication/authorization)
- **Docker Compose** (orchestration)
- **Serilog** (structured logging)

## Key Features

### 1. Event-Driven Architecture
- Services communicate asynchronously via RabbitMQ
- `ProductCreatedEvent` published by ProductService, consumed by StockService to sync read model
- `ProductInventoryAddedEvent` published by StockService, consumed by ProductService to update amounts
- No synchronous HTTP dependencies between services

### 2. Idempotency
- ProductService tracks processed EventIds in database
- Duplicate events are detected and skipped
- Ensures product amounts don't double-increment on message replay

### 3. Authentication & Authorization
- JWT-based authentication
- Role-based authorization (read/write roles)
- Shared secret key across services

### 4. Clean Architecture
- Domain, Application, Infrastructure layers
- Repository pattern
- Dependency injection

### 5. Local Read Models
- StockService maintains local `ProductReadModels` table synced from ProductService
- Product validation uses local data (no cross-service HTTP calls)
- Improves resilience and performance

## Project Structure

```
net-recruitment-task/
├── src/
│   ├── Shared/
│   │   └── Contracts/Events/
│   │       ├── ProductCreatedEvent.cs
│   │       └── ProductInventoryAddedEvent.cs
│   ├── ProductService/
│   │   ├── ProductService.API/
│   │   ├── ProductService.Application/
│   │   │   └── Consumers/ProductInventoryAddedHandler.cs (idempotency)
│   │   ├── ProductService.Domain/
│   │   └── ProductService.Infrastructure/
│   │       └── IdempotencyTracking/ProcessedEvent.cs
│   └── Stock/
│       ├── Stock.API/
│       ├── Stock.Application/
│       │   ├── Consumers/ProductCreatedHandler.cs (read model sync)
│       │   └── Services/StockServiceImpl.cs (event publisher)
│       ├── Stock.Domain/
│       │   ├── Entities/ProductReadModel.cs
│       │   └── IProductChecker.cs
│       └── Stock.Infrastructure/
│           └── Repositories/ProductReadModelRepository.cs
├── docker-compose.yml
├── Dockerfile.ProductService
└── Dockerfile.StockService
```

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### Running with Docker Compose

1. **Clone the repository**
```bash
git clone <repo-url>
cd net-recruitment-task
```

2. **Start all services**
```bash
docker-compose up --build
```

This starts:
- ProductService API (http://localhost:5044)
- StockService API (http://localhost:5132)
- PostgreSQL for ProductService (port 5434)
- PostgreSQL for StockService (port 5433)
- RabbitMQ (port 5672, management UI: http://localhost:15672)

3. **Check service health**
- ProductService Swagger: http://localhost:5044/swagger
- StockService Swagger: http://localhost:5132/swagger
- RabbitMQ Management: http://localhost:15672 (guest/guest)

### Running Locally (without Docker)

1. **Start infrastructure**
```bash
# Start PostgreSQL and RabbitMQ only
docker compose up -d postgres-product postgres-inventory rabbitmq
```

2. **Run ProductService**
```bash
cd src/ProductService/ProductService.API
dotnet run
# Runs on http://localhost:5044
```

3. **Run StockService** (in new terminal)
```bash
cd src/Stock/Stock.API
dotnet run
# Runs on http://localhost:5132
```

## API Endpoints

### ProductService (port 5044)

#### Create Product
```http
POST /products
Authorization: Bearer <token-with-write-role>
Content-Type: application/json

{
  "name": "Gaming Laptop",
  "description": "High-performance laptop",
  "price": 1500.00
}
```

#### Get All Products
```http
GET /products
Authorization: Bearer <token-with-read-role>
```

#### Get Product by ID
```http
GET /products/{id}
Authorization: Bearer <token-with-read-role>
```

### StockService (port 5132)

#### Add Inventory
```http
POST /inventory
Authorization: Bearer <token-with-write-role>
Content-Type: application/json

{
  "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "quantity": 10
}
```

## JWT Token Generation

The system requires JWT tokens for authentication. Here's how to generate tokens for testing:

### Using GenerateTokens.csx Script (Recommended)

The repository includes `GenerateTokens.csx` that generates tokens for both services:

```bash
dotnet script GenerateTokens.csx
```

Output includes:
- **ProductService token** (read + write roles)
- **StockService token** (read + write roles)
- Usage examples with curl commands

Tokens are valid for 24 hours with the shared secret: `your-super-secret-key-min-32-chars-long-for-security`

### Online JWT Generator (Quick Testing)

Use [jwt.io](https://jwt.io) with these settings:

**Payload:**
```json
{
  "sub": "user123",
  "role": "write",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "user123",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "write",
  "exp": 1735689600,
  "iss": "ProductService",
  "aud": "ProductService"
}
```

**Secret:** `your-super-secret-key-min-32-chars-long-for-security`

## Testing the Full Flow

### 1. Generate JWT Token
```bash
# Generate token with write role (see section above)
export TOKEN="<your-jwt-token>"
```

### 2. Create a Product
```bash
curl -X POST http://localhost:5044/products \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Gaming Laptop",
    "description": "High-performance gaming laptop with RTX 4080",
    "price": 2500.00
  }'
```

Response:
```json
{
  "id": "e3b9c44d-9b72-4d5e-9f8a-1c2d3e4f5a6b",
  "name": "Gaming Laptop",
  "description": "High-performance gaming laptop with RTX 4080",
  "price": 2500.00,
  "amount": 0,
  "createdAt": "2026-02-08T12:00:00Z",
  "updatedAt": "2026-02-08T12:00:00Z"
}
```

### 3. Get Products (verify initial amount is 0)
```bash
curl http://localhost:5044/products
```

### 4. Add Inventory (triggers event)
```bash
curl -X POST http://localhost:5132/inventory \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "e3b9c44d-9b72-4d5e-9f8a-1c2d3e4f5a6b",
    "quantity": 50
  }'
```

### 5. Verify Product Amount Updated
```bash
# Wait 1-2 seconds for event processing
sleep 2

curl http://localhost:5044/products/e3b9c44d-9b72-4d5e-9f8a-1c2d3e4f5a6b
```

Expected: `"amount": 50`

### 6. Test Idempotency

Check RabbitMQ management UI (http://localhost:15672):
1. Go to Queues tab
2. Find the ProductInventoryAddedEvent queue
3. Manually publish the same event twice
4. Verify product amount only increments once

## Verifying Idempotency

The ProductService tracks processed EventIds. To verify:

1. **Check ProcessedEvents table**
```bash
docker exec -it postgres-product psql -U postgres -d productdb -c "SELECT * FROM \"ProcessedEvents\";"
```

2. **Check logs for duplicate detection**
```bash
docker logs productservice-api | grep "already processed"
```

## Database Migrations

Migrations are applied automatically on startup. To create new migrations:

```bash
# ProductService
cd src/ProductService/ProductService.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../ProductService.API

# StockService
cd src/Stock/Stock.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../Stock.API
```

## Configuration

### Environment Variables

Both services support configuration via environment variables (see `docker-compose.yml`):

- `ConnectionStrings__DefaultConnection`: PostgreSQL connection string
- `RabbitMQ__Host`: RabbitMQ host
- `RabbitMQ__Username`: RabbitMQ username
- `RabbitMQ__Password`: RabbitMQ password
- `Jwt__Secret`: JWT signing key (must be same across services)
- `Jwt__Issuer`: JWT issuer
- `Jwt__Audience`: JWT audience
- `ProductService__Url`: ProductService base URL (StockService only)

## Troubleshooting

### Services won't start
```bash
# Check logs
docker-compose logs productservice-api
docker-compose logs stockservice-api

# Restart services
docker-compose down
docker-compose up --build
```

### Database connection issues
```bash
# Verify PostgreSQL is running
docker ps | grep postgres

# Check database connectivity
docker exec -it postgres-product psql -U postgres -d productdb -c "SELECT 1;"
```

### RabbitMQ connection issues
```bash
# Check RabbitMQ is running
docker ps | grep rabbitmq

# Check RabbitMQ logs
docker logs rabbitmq
```

### JWT authentication fails
- Verify token is not expired
- Ensure secret key matches in both services
- Check issuer/audience match configuration

## Architecture Decisions

### Why Wolverine for messaging?
- Lightweight and performant
- Native .NET integration
- Simplified configuration compared to alternatives
- Built-in support for RabbitMQ transport

### Why separate databases?
- Microservices principle: each service owns its data
- Enables independent scaling
- Reduces coupling between services

### Why local read models instead of HTTP calls?
- Eliminates synchronous coupling between services
- Each service can operate independently (better resilience)
- Improved performance (local database query vs HTTP call)
- Eventually consistent data is acceptable for product existence validation

### Why in-database idempotency tracking?
- Transactional consistency (EventId + Product update in same transaction)
- No external dependencies required
- Simple and reliable

## Future Enhancements

- basic CQRS (command/query dispathers) instead of direct using StockService in Controller.
- Add retry policies with Polly
- Implement distributed tracing (OpenTelemetry)
- Service-to-service authentication (not JWT from client)
