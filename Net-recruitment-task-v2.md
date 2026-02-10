# Task for .NET Developers

This assignment is designed to evaluate your ability to build a small distributed system using modern .NET practices, message-driven architecture, and clean application design. The goal is to implement two cooperating services that communicate using an event bus, maintain consistent data, and support resilient processing with proper error handling and idempotency, as well as proper authentication/authorization.

You will demonstrate skills in:

* API design with authentication and authorization
* JWT token validation & role-based access control (RBAC)
* Messaging patterns (publish/consume)
* Separation of concerns and clean architecture
* Dockerization
* Integration/functional and end-to-end testing
* Ensuring message idempotency
* Error structuring and logging

---

## 📌 High-Level Scenario

We simulate a simple warehouse/product system:

1. A product is created in **ProductService**.
2. An inventory entry is added in **InventoryService**.
3. InventoryService publishes an event `ProductInventoryAdded` which contains the amount of some product added to our inventory. Note that we will only be incrementing the product count using this event.
4. ProductService consumes the event and updates the product’s `Amount` field in product entity.

The services should be independent, communicate only through the message bus, and remain consistent even if events are re-delivered or processed out of order.

---

## 🧱 Architectural Assumptions

* All API endpoints must be secured with JWT authentication.
* Use two roles for both services:
  * `write` → allowed to perform all POST operations
  * `read` → allowed to perform all GET operations
* The application must follow **layered architecture** (e.g., API → Application → Domain → Infrastructure).
* Use **any SQL database** (SQL Server, PostgreSQL, MySQL, etc.).
* Use **MassTransit or Wolverine** as the message bus abstraction.
* Everything must run locally using **Docker Compose** or **Aspire**.
* All errors must be logged, structured, and traceable.
* Event processing must be **idempotent** to avoid double updates.

---

## 🛠 Service 1: InventoryService

### Responsibilities

Handles inventory additions and publishes domain events.

### Requirements

* Expose a `POST /inventory` endpoint to add inventory entries.
  * Requires `write` role
  * Implement data validation
    - `Quantity` > 0
    - `ProductId` must exists
* Use a repository with an `Insert` method to persist entries.
* After insertion, publish an event (**ProductInventoryAddedEvent**) to the bus.

### Domain Entity
```csharp
public class Inventory
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; }
    public string AddedBy { get; set; }
}
```
---

## 🛠 Service 2: ProductService

### Responsibilities

Manages product metadata and reacts to inventory added events.

### Requirements

* `POST /products` endpoint to add a product.
  * Requires write role
  * Implement data validation
    - `Name` required
    - `Price` > 0
* `GET /products` endpoint to retrieve products.
  * Requires `read` role
* Implement a consumer that listens for `ProductInventoryAddedEvent` events.
  * Update the product’s `Amount`.
  * Ensure idempotency so that the same event processed twice results in a **single update**.

### Domain Entity
```csharp
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public int Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```
---

## Data Contract
```csharp
public record ProductInventoryAddedEvent
{
    public Guid EventId { get; init; }
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public DateTime OccurredAt { get; init; }      
}
```
---

## 🧪 Integration/functional Testing

### Expected tests

#### Producer tests (InventoryService)

* When calling `POST /inventory`, the event should be sent to the message bus and endpint should return `201` status code.

#### Consumer tests (ProductService)

* When the same event is delivered twice:

  * Product amount in the database should update **only once**.
---

## 🧪 End-to-End Testing

Using **TestContainers** & **WebApplicationFactory**:

1. Call `POST /inventory` in the InventoryService.
2. Verify that ProductService has increased the product amount accordingly.
3. The test should spin up the full environment (both services + message bus + database)

---

## 🎯 What We Expect from Your Solution

* Clean, maintainable code following good .NET practices.
* Clear separation between layers.
* Proper use of MassTransit or Wolverine.
* Structured logging and error handling.
* Working integration/functional and E2E tests.
* Demonstrated understanding of event-driven architecture.

---

## How to Submit

1. Create a **public GitHub repo** (recommended).
2. Put all code + README there.
3. Share the link with us. We’ll review and then discuss it with you.
