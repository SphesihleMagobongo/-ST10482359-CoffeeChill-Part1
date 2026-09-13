# CoffeeNChill — Part 1: Azure Functions, Tables, Files & Docker Hub

Cloud foundation for the **CoffeeNChill** campus canteen. Paper menus and filing-cabinet
documents are replaced by **Azure Table Storage** (dynamic menu), a **`staff-docs` file
share** (operational documents) and **HTTP-triggered Azure Functions**, all running
against local **Azurite** storage emulation and packaged as standalone **Docker**
containers (no Compose in Part 1).

**Video demonstration (unlisted YouTube):** `<ADD_YOUTUBE_LINK_HERE>`

---

## 1. Architecture Overview

```
                    ┌───────────────────────────────┐
   Postman /        │  coffeenchill-functions:v1.0   │
   Browser /        │  Azure Functions (.NET 8,      │
   curl   ──HTTP──► │  isolated worker)              │
    :7071           │                                │
                    │  MenuFunctions ──────────┐     │
                    │  DocumentFunctions ────┐ │     │
                    └────────────────────────│─│─────┘
                                             │ │
                    ┌────────────────────────▼─▼─────┐
                    │        Azurite container        │
                    │  Table svc :10002  MenuItems    │
                    │  Blob  svc :10000  staff-docs   │
                    │  Queue svc :10001  (Functions   │
                    │                     host state) │
                    └────────────────────────────────┘
```

- **MenuItems table** — `PartitionKey = Category`, `RowKey = SKU`, plus `Name`,
  `Description`, `Price`, `IsAvailable`.
- **staff-docs share** — uploaded files are streamed to storage with
  `Content-Type`, size and last-modified metadata tracked per document.
- **Storage abstraction** — `IMenuItemRepository` and `IStaffDocumentStore` keep
  all Azure SDK calls out of the HTTP layer.

> **Note on Azurite:** Azurite emulates Blob, Queue and Table storage only — it
> does not emulate the Azure Files service. For local development `staff-docs`
> is therefore backed by a Blob *container* of the same name with identical
> streaming/metadata semantics (`AzuriteDocumentStore`). A real Azure Files
> `ShareClient` implementation (`AzureFilesDocumentStore`) ships alongside it —
> set `Storage:Mode=AzureFiles` with a real connection string to use a genuine
> file share, with no changes to the HTTP layer.

---

## 2. Project Structure

```
├── CoffeeNChill.slnx
├── README.md
├── docs/
│   ├── CoffeeNChill-Part1.postman_collection.json
│   ├── CoffeeNChill-Local.postman_environment.json
│   └── sample-docs/                     # files to test uploads with
│       ├── espresso-recipe.txt
│       └── equipment-cleaning-checklist.md
└── src/
    └── CoffeeNChill.Functions/
        ├── CoffeeNChill.Functions.csproj
        ├── Program.cs                   # DI wiring / storage-mode switch
        ├── host.json
        ├── local.settings.json          # Azurite connection strings
        ├── Dockerfile                   # multi-stage build
        ├── .dockerignore
        ├── Models/                      # MenuItemEntity + DTOs (validation)
        ├── Services/                    # table repo + document stores
        ├── Helpers/                     # JSON/validation/error helpers
        └── Functions/                   # HTTP triggers
            ├── MenuFunctions.cs
            ├── DocumentFunctions.cs
            └── HealthFunction.cs
```

---

## 3. API Reference

| Method | Endpoint                              | Function                | Description                        |
| ------ | ------------------------------------- | ----------------------- | ---------------------------------- |
| POST   | `/api/menu`                           | `CreateMenuItem`        | Insert a menu entity               |
| GET    | `/api/menu`                           | `GetAllMenuItems`       | Return all menu entities           |
| GET    | `/api/menu/category/{category}`       | `GetMenuItemsByCategory`| Filter by `PartitionKey`           |
| PUT    | `/api/menu/{category}/{id}`           | `UpdateMenuItem`        | Update price / availability        |
| DELETE | `/api/menu/{category}/{id}`           | `DeleteMenuItem`        | Remove an item                     |
| POST   | `/api/documents/upload`               | `UploadStaffDocument`   | multipart/form-data file upload    |
| GET    | `/api/documents`                      | `ListStaffDocuments`    | List files + size + last modified  |
| GET    | `/api/documents/download/{fileName}`  | `DownloadStaffDocument` | Stream a document back             |
| GET    | `/api/health`                         | `HealthCheck`           | Liveness probe                     |

**Create body** (`POST /api/menu`):

```json
{
  "category": "Hot Drinks",
  "sku": "COF-001",
  "name": "Espresso",
  "description": "Double shot espresso, rich crema",
  "price": 35.50,
  "isAvailable": true
}
```

**Update body** (`PUT /api/menu/{category}/{id}`) — at least one field required:

```json
{ "price": 38.00, "isAvailable": false }
```

Errors return a consistent JSON shape — `{"status": 400, "message": "...", "errors": [...]}`
with `400` (validation), `404` (missing entity/document) and `409` (duplicate SKU).

---

## 4. Local Development (no Docker)

### Prerequisites

| Tool                       | Install                                                        |
| -------------------------- | -------------------------------------------------------------- |
| .NET 8 SDK                 | https://dotnet.microsoft.com/download                          |
| Azure Functions Core Tools | `npm install -g azure-functions-core-tools@4 --unsafe-perms true` |
| Azurite                    | `npm install -g azurite`                                       |

### Steps

```bash
# Terminal 1 — storage emulator (Blob :10000, Queue :10001, Table :10002)
azurite --location ./azurite-data --silent

# Terminal 2 — Functions host on http://localhost:7071
cd src/CoffeeNChill.Functions
func start
```

`local.settings.json` already points at Azurite (`UseDevelopmentStorage=true`).

---

## 5. Standalone Docker Execution (Part 1 — no Compose)

### 5.1 Azurite container

```bash
docker run -d --name azurite \
  -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
```

### 5.2 Build the Functions image

```bash
cd src/CoffeeNChill.Functions
docker build -t <dockerhub_username>/coffeenchill-functions:v1.0 .
```

### 5.3 Run the Functions container

The two containers must be able to reach each other, so we put them on a
user-defined Docker network and point `AzureWebJobsStorage` at the `azurite`
hostname (`UseDevelopmentStorage=true` inside a container would resolve to the
container itself — see note below):

```bash
docker network create coffeenchill-net

docker rm -f azurite 2>$null; docker run -d --name azurite --network coffeenchill-net \
  -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite

docker run -d --name coffeenchill-functions --network coffeenchill-net -p 7071:80 \
  -e AzureWebJobsStorage="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;QueueEndpoint=http://azurite:10001/devstoreaccount1;TableEndpoint=http://azurite:10002/devstoreaccount1;" \
  -e Storage__Mode=Azurite \
  <dockerhub_username>/coffeenchill-functions:v1.0
```

> On Docker Desktop (Windows/Mac) you can alternatively keep the published ports
> and substitute `host.docker.internal` for `azurite` in the three endpoints —
> no network needed. Linux hosts need the network approach (or
> `--add-host=host.docker.internal:host-gateway`).

Verify:

```bash
curl http://localhost:7071/api/health        # {"status":"Healthy",...}
curl http://localhost:7071/api/menu
```

### 5.4 Publish to Docker Hub

```bash
docker login

# Functions image
docker push <dockerhub_username>/coffeenchill-functions:v1.0

# Azurite image — tag the official image under your account
docker pull mcr.microsoft.com/azure-storage/azurite
docker tag mcr.microsoft.com/azure-storage/azurite <dockerhub_username>/coffeenchill-azurite:v1.0
docker push <dockerhub_username>/coffeenchill-azurite:v1.0
```

Public repositories:
- `https://hub.docker.com/r/<dockerhub_username>/coffeenchill-functions` — tag `v1.0`
- `https://hub.docker.com/r/<dockerhub_username>/coffeenchill-azurite` — tag `v1.0`

---

## 6. Postman Testing

1. Import `docs/CoffeeNChill-Part1.postman_collection.json` (and optionally the
   `CoffeeNChill-Local` environment).
2. `baseUrl` is a collection variable — defaults to `http://localhost:7071`.
3. For the **Upload Staff Document** request, attach a file — samples live in
   `docs/sample-docs/` (the file field already references
   `espresso-recipe.txt`).
4. Run the folders in order — `1. Health & Setup`, `2. Menu Items`,
   `3. Staff Documents`. Tests assert status codes, response shapes and
   negative paths (400 / 404 / 409), and chain `createdSku`, `createdCategory`
   and `docFileName` between requests.
5. Or run the whole suite headlessly:

```bash
npx newman run docs/CoffeeNChill-Part1.postman_collection.json
```

---

## 7. Team Contributions

| Member | Student No. | Contributions |
| ------ | ----------- | ------------- |
| `<Name 1>` | `<no>` | MenuItems schema, menu CRUD functions, validation & error handling |
| `<Name 2>` | `<no>` | staff-docs document functions, streaming, MIME validation |
| `<Name 3>` | `<no>` | Dockerfile, Azurite orchestration, Docker Hub publishing, Postman suite |

---

## 8. Deliverables Checklist

- [x] `MenuItems` table — `PartitionKey` = category, `RowKey` = SKU
- [x] 5 menu HTTP functions (Create / GetAll / GetByCategory / Update / Delete)
- [x] 3 document HTTP functions (Upload / List / Download) with streaming
- [x] `local.settings.json` → `UseDevelopmentStorage=true` (Azurite)
- [x] Multi-stage `Dockerfile` on the official Azure Functions isolated base image
- [x] Standalone `docker run` commands (no Compose)
- [x] Postman collection + environment in `/docs`
- [ ] Docker Hub: `coffeenchill-functions:v1.0` and `coffeenchill-azurite:v1.0`
- [ ] YouTube demo link (unlisted) — `docker run` + Postman execution
- [ ] ≥ 5 meaningful commits per member
