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
- Docker (для RabbitMQ через Aspire)
- [Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`

## Запуск

```powershell
dotnet run --project RmqSAF-RmqRAP-Rest.AppHost
```

Aspire Dashboard откроется автоматически. Дождитесь статуса **Healthy** у `rabbitmq`, `storage`, `bridge`, `sender`.

## Проверка сценария

1. Откройте endpoint **sender** (в Dashboard → Endpoints).
2. В браузере откроется UI (`/index.html`) — отправьте JSON payload.
3. Скопируйте `correlationId` из ответа.
4. Откройте **storage** → `GET /api/payloads?correlationId={id}` — payload должен появиться в списке.

### API вручную

```http
POST /api/messages
Content-Type: application/json

{
  "type": "SampleEvent",
  "data": { "key": "value" }
}
```

```http
GET /api/payloads
GET /api/payloads?correlationId={correlationId}
GET /api/payloads/{messageId}
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
| `Sender.Api/rabbitmq.json` | `E2EOut` | RabbitMQ publish |
| `Bridge.Worker/rabbitmq.json` | `E2EInbox` | RabbitMQ listener |
| `Bridge.Worker/rest.json` | `StorageIngest` | REST POST → storage |

Aspire подставляет connection string RabbitMQ через `RabbitMqAspireConfiguration`. Storage доступен по service discovery (`http://storage`).

## Observability

- Health: `/health`, `/health/ready` (Aspire ServiceDefaults)
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
