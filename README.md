# CoffeeNChill — Part 1: Azure Functions, Tables, Files & Docker Hub

HTTP-triggered Azure Functions (.NET 8, isolated worker) for the CoffeeNChill
canteen. Menu items are stored in Azure Table Storage and staff documents in a
`staff-docs` file share, all emulated locally with Azurite and packaged as
standalone Docker containers (no Compose).

**Video (unlisted YouTube):** 
https://youtu.be/1dkDmuSSHJI

**Docker Hub:** `sphesihlezorro/coffeenchill-functions:v1.0` and
`sphesihlezorro/coffeenchill-azurite:v1.0`

## Endpoints

- `POST /api/menu` — create item
- `GET /api/menu` — list all items
- `GET /api/menu/category/{category}` — filter by PartitionKey
- `PUT /api/menu/{category}/{id}` — update price / availability
- `DELETE /api/menu/{category}/{id}` — delete item
- `POST /api/documents/upload` — multipart/form-data upload
- `GET /api/documents` — list files (name, size, last modified)
- `GET /api/documents/download/{fileName}` — stream a file back
- `GET /api/health` — liveness check

The `MenuItems` table uses `PartitionKey = category` and `RowKey = SKU`.
Azurite does not emulate Azure Files, so `staff-docs` is backed by a Blob
container locally (`AzuriteDocumentStore`); a real `ShareClient`
implementation (`AzureFilesDocumentStore`) is included for production via
`Storage:Mode=AzureFiles`.

## Local dev (no Docker)

Prerequisites: .NET 8 SDK, `npm i -g azurite azure-functions-core-tools@4`.

```bash
azurite --location ./azurite-data --silent
cd src/CoffeeNChill.Functions && func start
```

`local.settings.json` already points to Azurite (`UseDevelopmentStorage=true`).

## Docker (standalone, no Compose)

```bash
docker network create coffeenchill-net

docker run -d --name azurite --network coffeenchill-net \
  -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite

cd src/CoffeeNChill.Functions
docker build -t sphesihlezorro/coffeenchill-functions:v1.0 .

docker run -d --name coffeenchill-functions --network coffeenchill-net -p 7071:80 \
  -e AzureWebJobsStorage="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;QueueEndpoint=http://azurite:10001/devstoreaccount1;TableEndpoint=http://azurite:10002/devstoreaccount1;" \
  -e Storage__Mode=Azurite \
  sphesihlezorro/coffeenchill-functions:v1.0
```

The shared network is needed because `UseDevelopmentStorage=true` inside a
container would resolve to the container itself; the connection string points
at the `azurite` hostname instead. App runs on `http://localhost:7071`.

Publish to Docker Hub:

```bash
docker login
docker push sphesihlezorro/coffeenchill-functions:v1.0
docker tag mcr.microsoft.com/azure-storage/azurite sphesihlezorro/coffeenchill-azurite:v1.0
docker push sphesihlezorro/coffeenchill-azurite:v1.0
```

## Testing

Import `docs/CoffeeNChill-Part1.postman_collection.json` into Postman and run
the collection — 19 requests with automated tests covering every endpoint plus
400/404/409 error paths. Or headless: `cd docs && npx newman run
CoffeeNChill-Part1.postman_collection.json`.

## Contribution

Completed individually by **Sphesihle Magobongo (ST10482359)** — all function
code, storage layer, Dockerfile, Docker Hub publishing, Postman suite and
documentation.
