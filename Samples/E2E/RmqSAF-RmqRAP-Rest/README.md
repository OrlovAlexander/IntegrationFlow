# RmqSAF-RmqRAP-Rest — E2E sample

End-to-end демонстрация цепочки **IntegrationFlow**:

**Sender (SAF)** → RabbitMQ → **Bridge (RAP)** → REST → **Storage**

| Сервис | Паттерн | Роль |
|--------|---------|------|
| `Sender.Api` | SentAndForgot | Web UI + publish в `integration.e2e.inbox` |
| `Bridge.Worker` | ReceiveAndProcess | Consumer RabbitMQ → REST forward |
| `Storage.Api` | REST receiver | In-memory store + GET API |

## Требования

- .NET 10 SDK
- Docker

## Запуск

### Вариант A: Aspire (локальная разработка)

- [Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`

```powershell
dotnet run --project RmqSAF-RmqRAP-Rest.AppHost
```

Aspire Dashboard откроется автоматически. Дождитесь статуса **Healthy** у `rabbitmq`, `storage`, `bridge`, `sender`.

### Вариант B: Docker Compose (без Aspire)

Поднимает RabbitMQ и все три сервиса в контейнерах:

```powershell
docker compose up --build
```

Фоновый режим: `docker compose up --build -d`. Остановка: `docker compose down`.

| Сервис | URL / порт |
|--------|------------|
| Sender UI | http://localhost:8080 |
| RabbitMQ AMQP | `localhost:5673` (в контейнерах — `rabbitmq:5672`) |
| RabbitMQ Management | http://localhost:15673 (логин `e2e` / `e2e`) |

Проверка статуса контейнеров:

```powershell
docker compose ps
```

Все сервисы должны перейти в **healthy** (обычно 30–60 с после старта).

**Сборка образов:** общий `Dockerfile` в корне sample; контекст сборки — корень репозитория (`../../..`). Переменные `PROJECT_PATH` и `ENTRY_ASSEMBLY` задают, какой проект публикуется в образ.

**Переопределения в `docker-compose.yml`:** `ConnectionStrings__rabbitmq`, `RabbitMq__E2EInbox__*`, `RabbitMqPublish__E2EOut__*`, `RestConnections__StorageApi__BaseAddress=http://storage:8080` — те же профили `E2EOut` / `E2EInbox` / `StorageIngest`, что и при запуске через AppHost.

**Healthcheck в Compose:** для `bridge` и `sender` используется `/alive` (liveness), а не `/health`. Endpoint `/health` включает integration readiness checks (RabbitMQ, REST) и может отвечать дольше; для проверки «процесс поднялся и слушает порт» достаточно `/alive`.

## Проверка сценария

### Через UI

1. Откройте **sender** — http://localhost:8080 (Compose) или endpoint в Aspire Dashboard.
2. В браузере откроется UI (`/index.html`) — отправьте JSON payload.
3. Скопируйте `correlationId` из ответа.
4. Проверьте payload в storage (см. API ниже).

### API вручную

```http
POST /api/messages
Content-Type: application/json

{
  "type": "SampleEvent",
  "data": { "key": "value" }
}
```

Ответ: `messageId`, `correlationId`, `status: accepted`.

```http
GET /api/payloads
GET /api/payloads?correlationId={correlationId}
GET /api/payloads/{messageId}
```

Пример (PowerShell, Compose):

```powershell
$body = '{"type":"SampleEvent","data":{"key":"value"}}'
$resp = Invoke-RestMethod -Method POST -Uri http://localhost:8080/api/messages `
    -ContentType application/json -Body $body
$cid = $resp.correlationId

# из контейнера storage
docker exec rmq-saf-rap-rest-e2e-storage-1 curl -fsS `
    "http://127.0.0.1:8080/api/payloads?correlationId=$cid"
```

Ожидаемый результат: в storage появляется запись с тем же `correlationId` и телом сообщения; в RabbitMQ очередь `integration.e2e.inbox` пуста, у bridge — 1 consumer:

```powershell
docker exec rmq-saf-rap-rest-e2e-rabbitmq-1 rabbitmqctl list_queues name messages consumers
```

## Топология

```
Browser → Sender.Api (SAF publish)
              ↓
         RabbitMQ queue: integration.e2e.inbox
              ↓
         Bridge.Worker (RAP consumer → REST POST)
              ↓
         Storage.Api POST /api/payloads → in-memory store
```

## Конфигурация

| Файл | Профиль | Назначение |
|------|---------|------------|
| `Sender.Api/rabbitmq.json` | `E2EOut` | RabbitMQ publish (`DeclareTopologyOnStartup: true` — очередь создаётся при первой публикации) |
| `Bridge.Worker/rabbitmq.json` | `E2EInbox` | RabbitMQ listener (`DeclareTopologyOnStartup: true`) |
| `Bridge.Worker/rest.json` | `StorageIngest` | REST POST → storage |

Локальные значения в `rabbitmq.json` (`localhost`, `guest`) переопределяются в Compose и AppHost через переменные окружения / connection string.

Aspire подставляет connection string RabbitMQ через `RabbitMqAspireConfiguration`. В Compose storage доступен по DNS-имени сервиса (`http://storage:8080`).

## Observability

| Endpoint | Назначение |
|----------|------------|
| `/alive` | Liveness (`self` check) — используется healthcheck в Docker Compose |
| `/health` | Все health checks, включая IntegrationFlow (RabbitMQ listener, REST) |

- OpenTelemetry metrics: `AddIntegrationFlowOpenTelemetryMetrics()`
- Traces/logs: Aspire Dashboard → **Traces** / **Structured logs**, фильтр по `correlationId`

## Сборка отдельных проектов

```powershell
dotnet build RmqSAF-RmqRAP-Rest.Contracts
dotnet build RmqSAF-RmqRAP-Rest.ServiceDefaults
dotnet build Storage.Api
dotnet build Bridge.Worker
dotnet build Sender.Api
dotnet build RmqSAF-RmqRAP-Rest.AppHost
```

Подробный план: [docs/implementation-plan.md](docs/implementation-plan.md).
