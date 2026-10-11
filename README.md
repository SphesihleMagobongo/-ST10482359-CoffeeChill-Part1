# CoffeeNChill — Azure Functions, Storage & Docker (Parts 1 + 2)

HTTP- and queue-triggered Azure Functions (.NET 8, isolated worker) for the
CoffeeNChill canteen. Menu items live in Azure Table Storage, staff documents
in `staff-docs`, and orders flow asynchronously through the
`order-processing-queue` Storage Queue into an `Orders` table. Everything runs
locally on Azurite, orchestrated by a single `docker-compose.yml`.

**Part 1 video:**
 https://youtu.be/1dkDmuSSHJI

**Part 2 video:**
https://youtu.be/5m_itxgW_XU


**Docker Hub:** `sphesihlezorro/coffeenchill-functions:v2.0` and
`sphesihlezorro/coffeenchill-azurite:v2.0`

## Part 2 — Async order processing

- `POST /api/orders/queue` — validates the order, generates `OrderId` +
  `OrderTimestamp`, serializes the payload to JSON, Base64-encodes it and
  pushes it onto `order-processing-queue`. Returns `202`.
- `ProcessOrderQueue` — `[QueueTrigger("order-processing-queue")]` decodes the
  message, writes the order to the `Orders` table (`PartitionKey` = order
  date, `RowKey` = `OrderId`, `Status` = `Received`), then walks the status
  through `Received -> Preparing -> Ready -> Collected` with a log line per
  transition. Malformed payloads and storage failures are logged and thrown,
  so bad messages land in `order-processing-queue-poison` after the runtime's
  retries.
- `GET /api/orders` / `GET /api/orders/{id}` — read back order status live.

## One-command startup (Part 2)

```bash
docker compose up -d
```

The compose file pulls `sphesihlezorro/coffeenchill-functions:v2.0` and
`sphesihlezorro/coffeenchill-azurite:v2.0` straight from Docker Hub, creates a
custom bridge network (`coffeenchill-net`) and a persistent volume
(`coffeenchill-azurite-data`) for Azurite's data. The functions service waits
for the Azurite healthcheck before starting. App runs on
`http://localhost:7071`; `docker compose down` stops everything.

## Endpoints

Menu (Part 1): `POST/GET /api/menu`, `GET /api/menu/category/{category}`,
`PUT/DELETE /api/menu/{category}/{id}`.
Documents (Part 1): `POST /api/documents/upload`, `GET /api/documents`,
`GET /api/documents/download/{fileName}`.
Orders (Part 2): `POST /api/orders/queue`, `GET /api/orders`,
`GET /api/orders/{id}`.
Health: `GET /api/health`.

## Testing

Import `docs/CoffeeNChill-Part1.postman_collection.json` into Postman and run
the whole collection — menu CRUD, document upload/download, order queueing,
plus polling checks that wait for the queued order to appear in `Orders` and
reach `Collected`. Headless: `cd docs && npx newman run
CoffeeNChill-Part1.postman_collection.json`.

## Local dev (no Docker)

Prerequisites: .NET 8 SDK, `npm i -g azurite azure-functions-core-tools@4`.

```bash
azurite --location ./azurite-data --silent
cd src/CoffeeNChill.Functions && func start
```

## Files added for Part 2

All Part 2 code is prefixed `Part2` and marked with a `[Part2]` code attribute
(defined in `src/CoffeeNChill.Functions/Part2Attribute.cs`).

- `Functions/Part2OrderFunctions.cs` — `QueueOrder` (POST /api/orders/queue),
  `GetAllOrders`, `GetOrderById` HTTP functions.
- `Functions/Part2ProcessOrderQueueFunction.cs` — `ProcessOrderQueue`
  `[QueueTrigger("order-processing-queue")]` consumer with lifecycle
  transitions and poison-queue logging.
- `Services/Part2OrderQueueProducer.cs` + `Part2IOrderQueueProducer.cs` —
  JSON serialization, Base64 encoding, queue push, OrderId/timestamp
  generation.
- `Services/Part2OrderRepository.cs` + `Part2IOrderRepository.cs` — `Orders`
  table writes, status advancement, queries.
- `Models/Part2OrderEntity.cs` — `OrderEntity` + lifecycle status constants.
- `Models/Part2OrderDtos.cs` — `QueueOrderRequest` (validated),
  `OrderQueueMessage`, `OrderResponse`.
- `docker-compose.yml` — two-service orchestration pulling `v2.0` images from
  Docker Hub, custom bridge network, Azurite data volume, healthcheck.
- `docs/CoffeeNChill-Part1.postman_collection.json` — extended with the
  "4. Orders (Queue Processing)" folder.

## Changelog

- **Part 2** — added `QueueOrder` producer endpoint with validation +
  Base64 JSON payloads, `ProcessOrderQueue` queue trigger with full order
  lifecycle (Received -> Preparing -> Ready -> Collected) and poison-queue
  exception logging, `Orders` table repository, order status read endpoints,
  and `docker-compose.yml` orchestrating Azurite + the function host from
  Docker Hub `v2.0` images.
- **Part 1** — menu CRUD on Azure Tables, staff document upload/list/download
  (`staff-docs`), validation + 400/404/409 handling, multi-stage Dockerfile,
  standalone containers published to Docker Hub `v1.0`.

## Contribution

Completed individually by **Sphesihle Magobongo (ST10482359)** — all function
code, storage layer, Dockerfile + Compose, Docker Hub publishing, Postman
suite and documentation.
