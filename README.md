# Logistics and Order Delivery System

A .NET 10 CQRS and Event Sourcing solution for order delivery lifecycle management.

## Architecture

- **Logistics.Domain**: Aggregate root (`OrderAggregate`), domain events, exceptions, and `IEventStore` abstraction.
- **Logistics.Application**: Command/handler definitions and DispatchR-style CQRS routing (in-project implementation).
- **Logistics.Infrastructure.Write**: Event store using PostgreSQL (EF Core), `EventStoreDbContext`, `EventRecord`, and `IEventStore` implementation.
- **Logistics.Infrastructure.Read**: Read models and projection checkpoints using PostgreSQL (EF Core), `ReadDbContext`, `OrderDetailsView`, `ProblematicOrders`, `ProjectionCheckpoints`.
- **Logistics.WriteService**: Worker host that runs the RabbitMQ command consumer and the event projector background service. Uses Write DB and Read DB (for projections).
- **Logistics.ReadApi**: ASP.NET Core Minimal API for read-side queries only (Read DB).

## Databases

Use two **separate** PostgreSQL databases (e.g. `LogisticsWrite` and `LogisticsRead`). Set connection strings in `appsettings.json`:

- `ConnectionStrings:WriteDbConnection` (Write service and event store)
- `ConnectionStrings:ReadDbConnection` (Read API and projector read models)

Apply migrations (or ensure schema exists) for both:

- From **Logistics.Infrastructure.Write**: `EventStoreDbContext` (Events table with `GlobalSequence`, `AggregateId`, `Version`, `EventType`, `Data` jsonb, `CreatedAtUtc`).
- From **Logistics.Infrastructure.Read**: `ReadDbContext` (OrderDetailsView, ProblematicOrders, ProjectionCheckpoints).

## Running

1. **Write service** (consumes RabbitMQ, runs projector):
   ```bash
   dotnet run --project Logistics.WriteService
   ```

2. **Read API** (REST queries):
   ```bash
   dotnet run --project Logistics.ReadApi
   ```

## Endpoints (Read API)

- `GET /api/orders/{id}` – Order details and status history (US4).
- `GET /api/orders/failed` – Orders that failed delivery today (US5).

## Commands (via RabbitMQ)

Send JSON messages to the configured queues:

- **pack-order-commands**: `PackOrderCommand` – `{ "OrderId": "...", "WarehouseId": "...", "Weight": 1.5 }`
- **fail-delivery-commands**: `FailDeliveryCommand` – `{ "OrderId": "...", "Reason": "..." }`
- **change-address-commands**: `ChangeAddressCommand` – `{ "OrderId": "...", "NewAddress": "..." }`

Orders must be created first (e.g. by placing an order that appends `OrderPlaced` to the event store). The projector builds read models from the event store and keeps a checkpoint in `ProjectionCheckpoints`.
