# ![statevia icon](docs/images/icon-mark-32.png) statevia

[日本語](README.md) | English

**A platform for declaring workflows in YAML and running, visualizing and extending them end to end.**

The definition is the source of truth: statevia runs your business processes as **executable workflows**.
New here? Start with the [Quick Start](#quick-start).

| Execution graph | Definition editor |
| --- | --- |
| ![Execution graph of a parallel order workflow: one branch has shipped and the other is waiting for a stock transfer event](docs/images/screenshots/execution-graph.en.webp) | ![Definition editor in graph view, showing intake, validation, parallel shipping, join and notification nodes](docs/images/screenshots/definition-editor.en.webp) |

The screenshots use fictional data. The definition shown is [docs/samples/ui-customer-order-parallel.yaml](docs/samples/ui-customer-order-parallel.yaml). The Studio UI is in Japanese by default; switch to English with the **EN** button in the header.

---

## What you can do with statevia

| | |
| --- | --- |
| **Definition → execution** | Publish a YAML/JSON definition and start executions pinned to that version |
| **Durable execution** | Progress is persisted, so you can resume and inspect executions from the API or the UI |
| **Event driven** | States move on facts such as completed, failed and canceled |
| **Extensible actions** | Add and distribute business logic as Action Modules |
| **Visual workflow** | Follow executions on a live graph in the Studio UI |
| **Schedules** | Start executions at fixed times with a cron expression and a time zone |
| **Permissions and multi-tenancy** | Tenants, projects, roles and permission keys decide who can do what. API keys and service accounts let other systems call the API |
| **Execution history** | Starts, cancellations and events are kept in an append-only history that you can follow on a timeline |

You can build asynchronous and parallel business processes from the definition, with Fork / Join, Wait and cooperative cancellation.

---

## Quick Start

statevia has not reached its first stable release, so these steps may not work on every branch or at every point in time.

You will go from **clone → database → migration → all services → an execution**. The full guide (in Japanese) is [getting-started](docs/guides/getting-started.md).

**Prerequisites:** Docker and Docker Compose. The **.NET 10 SDK** to create the database the first time (`dotnet ef` runs on the host). **`jq`** for the curl examples below.

```bash
git clone https://github.com/statevia/statevia.git
cd statevia
cp .env.example .env          # first time only

docker compose up -d postgres
# First time only: create the schema in the empty PostgreSQL (the host connects, so the DB host is localhost)
set -a && source .env && set +a
export DATABASE_URL="postgres://${POSTGRES_USER}:${POSTGRES_PASSWORD}@localhost:${POSTGRES_PORT}/${POSTGRES_DB}"
(cd dotnet/service/api && dotnet ef database update --project Statevia.Service.Api)

docker compose up -d
```

If you already ran `docker compose up -d` and the API stopped, run `docker compose restart service-api` after the migration.

In this setup the Worker (advances executions) and the Scheduler (Wait deadlines and schedules) run inside the Service API process. To run them as separate processes (`compose/split-runtime.yml`) or to add log collection (Fluent Bit / Loki / Grafana in `compose/logging.yml`), see [operations-docker](docs/guides/operations-docker.md).

| Check | URL / command |
| --- | --- |
| API health | `curl -s http://localhost:8080/v1/health` |
| API reference | [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1) |
| Studio UI | [http://localhost:3000](http://localhost:3000) |

**Default development account** (created automatically when the API starts with `ASPNETCORE_ENVIRONMENT=Development`, after the first migration). Compose runs in Development, so this account is for your local machine only. It is never created outside Development. For a shared environment, create an administrator with [operations-tenant-bootstrap](docs/guides/operations-tenant-bootstrap.md).

| Item | Value |
| --- | --- |
| Tenant | `default` |
| Username | `admin` |
| Password | `admin123` |

**Run it once** (log in → prepare a project → register a definition → start an execution):

```bash
# Get a token
TOKEN=$(curl -s -X POST http://localhost:8080/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantKey":"default","username":"admin","password":"admin123"}' | jq -r .accessToken)

# The project that holds the definition (projectId is required to register one). Take slug=default from your projects
PROJECT_ID=$(curl -s "http://localhost:8080/v1/projects" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.[] | select(.slug=="default") | .projectId')

# A new environment has no project yet, so create one (the creator becomes its admin)
[ -n "$PROJECT_ID" ] || PROJECT_ID=$(curl -s -X POST "http://localhost:8080/v1/projects" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"slug":"default","displayName":"Default"}' | jq -r .projectId)

# Register the definition (POST. PUT only adds a version to an existing definition)
DEF_ID=$(curl -s -X POST "http://localhost:8080/v1/definitions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-customer-order-parallel.yaml \
    --arg projectId "$PROJECT_ID" \
    '{name:"my-workflow",yaml:$yaml,projectId:$projectId}')" | jq -r .displayId)

# Start an execution (definitionId is the displayId from the registration response)
curl -s -X POST "http://localhost:8080/v1/executions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: demo-1" \
  -d "{\"definitionId\":\"$DEF_ID\",\"input\":{}}"
```

In the UI, sign in at [http://localhost:3000/login](http://localhost:3000/login) with the same credentials.

To try only the Engine library: [hello-statevia](dotnet/core/engine/samples/hello-statevia) / [engine-standalone-guide](docs/guides/engine-standalone-guide.md)

---

## Documentation

The documentation under `docs/` is written in Japanese. Good places to start:

| Goal | Document |
| --- | --- |
| **Write a definition** | [Tutorial](docs/guides/first-workflow-tutorial.md) → [Definition specification](docs/specifications/definition.md) |
| **Call the HTTP API** | [HTTP request examples](docs/guides/http-request-examples.md) · [HTTP API specification](docs/specifications/api-http.md) |
| **Use the Studio UI** | [UI guide](docs/guides/ui-user-guide.md) |
| **Fix a problem** | [Troubleshooting](docs/guides/troubleshooting.md) · [Error codes](docs/reference/error-codes.md) |
| **Find everything else** | [docs/README.md](docs/README.md) |

---

## Repository layout

```text
statevia/
├─ dotnet/              # Everything in C# (solution and shared build settings)
│  ├─ core/             # Engine · Application
│  ├─ infrastructure/   # Persistence · Modules · Security
│  └─ service/          # Service API · CLI · action-host · runtime
├─ nodejs/
│  └─ studio/           # Web UI
└─ docs/                # Documentation (source of truth)
```

---

## License

MIT

---

## Status

statevia is under active design and development. Breaking changes may happen before the first stable release.
