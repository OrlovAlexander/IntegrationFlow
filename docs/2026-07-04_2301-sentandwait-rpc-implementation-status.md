# Статус реализации: SentAndWait RPC (фазы 1–2)

**Статус:** актуально  
**Создан:** 2026-07-04 23:01 (UTC+3)  
**Обновлён:** 2026-07-04 23:01 (UTC+3)  
**Связанные документы:** [`plans/2026-07-04_2242-sentandwait-rpc-critical-flows.md`](plans/2026-07-04_2242-sentandwait-rpc-critical-flows.md), [`plans/2026-07-04_2244-sentandwait-rpc-implementation.md`](plans/2026-07-04_2244-sentandwait-rpc-implementation.md), [`2026-07-04_2234-integrationflow-full-analysis.md`](2026-07-04_2234-integrationflow-full-analysis.md)

Итог реализации по плану [`2244-sentandwait-rpc-implementation.md`](plans/2026-07-04_2244-sentandwait-rpc-implementation.md). **117 unit-тестов** зелёные (Release); integration-тесты RPC — при наличии Docker.

---

## 1. Фаза 1 — Idempotent Sync RPC ✅

### Client

| Компонент | Реализация |
|-----------|------------|
| `TransmitData.MessageId` | [`SentAndWait/TransmitData.cs`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/SentAndWait/TransmitData.cs) |
| `SentAndWaitIntegration.WithMessageId()` | [`SentAndWaitIntegration.cs`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/SentAndWait/SentAndWaitIntegration.cs) |
| Retry после timeout | [`SentAndWaitIntegrationOptions`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/SentAndWait/SentAndWaitIntegrationOptions.cs): `RetryOnTimeout`, `MaxRetries`, `RetryDelay` |
| Transmitter | [`RabbitMqRequestReplyTransmitter.cs`](../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/SentAndWait/Transmitters/RabbitMqRequestReplyTransmitter.cs) — тот же MessageId, новый CorrelationId |

### Server response cache

| Компонент | Реализация |
|-----------|------------|
| `IRequestReplyResponseStore` | [`03Domain/SentAndWait/ResponseCache/`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/SentAndWait/ResponseCache/) |
| InMemory (sample/tests) | [`InMemoryRequestReplyResponseStore.cs`](../src/IntegrationFlow.Core/Contexts/Integrations/00Samples/SentAndWait/ResponseCache/InMemoryRequestReplyResponseStore.cs) |
| EF store | [`EfRequestReplyResponseStore.cs`](../src/IntegrationFlow.EntityFrameworkCore/ResponseCache/EfRequestReplyResponseStore.cs) |
| Pipeline | [`RabbitMqRpcServerPipeline.cs`](../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/SentAndWait/Reply/RabbitMqRpcServerPipeline.cs) |
| Sample server | [`SampleRabbitMqSentAndWaitRpcServer.cs`](../src/IntegrationFlow.Core/Contexts/Integrations/00Samples/SentAndWait/SampleRabbitMqSentAndWaitRpcServer.cs) |

### Metrics

| Instrument | Описание |
|------------|----------|
| `integrationflow.requestreply.retry_after_timeout` | Counter — retry после timeout (`profile`) |

DI: `AddIntegrationFlowEfRequestReplyResponseCache<TContext>()`.

### Тесты (фаза 1)

| Тест | Файл |
|------|------|
| `TransmitData.WithMessageId` | `tests/IntegrationFlow.Core.Tests/SentAndWait/SentAndWaitTransmitDataTests.cs` |
| InMemory response cache | `tests/IntegrationFlow.Core.Tests/SentAndWait/InMemoryRequestReplyResponseStoreTests.cs` |
| EF response cache | `tests/IntegrationFlow.EntityFrameworkCore.Tests/EfStoreTests.cs` (`EfRequestReplyResponseStoreTests`) |
| Retry metric | `tests/IntegrationFlow.Metrics.OpenTelemetry.Tests/OpenTelemetryIntegrationFlowMetricsTests.cs` |
| E2E cache + retry | `tests/IntegrationFlow.Core.IntegrationTests/RabbitMqRequestReplyEndToEndTests.cs` |

**DoD фазы 1:** выполнен.

---

## 2. Фаза 2 — Async Request-Response + Outbox (MVP каркас) ✅

Основное решение для **critical flows**: request staged в TX → relay → response queue → correlation.

### Domain

| Компонент | Файл |
|-----------|------|
| `RpcPendingRequest`, `RpcPendingStatus` | [`03Domain/RpcPending/`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/RpcPending/) |
| `IRpcPendingStore`, `IRpcPendingEnqueue` | там же |
| `RpcPendingRelayService` | [`RpcPendingRelayService.cs`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/RpcPending/RpcPendingRelayService.cs) |
| `RpcPendingWaitExtensions.WaitForCompletionAsync` | [`RpcPendingWaitExtensions.cs`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/RpcPending/RpcPendingWaitExtensions.cs) |

### Transport и конфиг

| Параметр | Описание |
|----------|----------|
| `RequestMode` | `Sync` (default) \| `AsyncOutbox` |
| `ResponseQueueName` | Очередь ответов (обязательна для AsyncOutbox) |
| `PendingTimeoutSeconds` | SLA ожидания ответа (default 300) |

Файл: [`RabbitMqRequestReplyConfiguration.cs`](../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/SentAndWait/Configurations/RabbitMqRequestReplyConfiguration.cs).

### Workers (.NET 8+)

| Worker | Назначение |
|--------|------------|
| `RpcPendingRelayBackgroundService` | Claim pending → publish на request queue |
| `RabbitMqRpcResponseCorrelationHostedService` | Consume response queue → `CompleteAsync` |

### EF

| Компонент | Файл |
|-----------|------|
| Entity | [`RpcPendingRequestEntity.cs`](../src/IntegrationFlow.EntityFrameworkCore/RpcPending/RpcPendingRequestEntity.cs) |
| Store + enqueue | [`EfRpcPendingStore.cs`](../src/IntegrationFlow.EntityFrameworkCore/RpcPending/EfRpcPendingStore.cs), [`EfRpcPendingEnqueue.cs`](../src/IntegrationFlow.EntityFrameworkCore/RpcPending/EfRpcPendingEnqueue.cs) |
| Extension | [`DbContextRpcPendingExtensions.EnqueueRpcRequest()`](../src/IntegrationFlow.EntityFrameworkCore/RpcPending/DbContextRpcPendingExtensions.cs) |
| Model | `ConfigureIntegrationFlow()` — таблица `IntegrationFlowRpcPendingRequests` |

### DI

```csharp
services.AddIntegrationFlowEfRpcPending<MyDbContext>();
services.AddIntegrationFlowRpcPendingRelay(options => { ... });
services.AddIntegrationFlowRabbitMqRpcResponseCorrelation(); // net8.0
```

### Пример AsyncOutbox

**Конфиг** (`rabbitmq.json`):

```json
{
  "RabbitMqRequestReply": {
    "OrdersRpc": {
      "RequestMode": "AsyncOutbox",
      "ResponseQueueName": "orders.rpc.responses",
      "QueueName": "orders.rpc.requests",
      "PendingTimeoutSeconds": 300
    }
  }
}
```

**Application TX:**

```csharp
var pending = db.EnqueueRpcRequest("OrdersRpc", payload);
await db.SaveChangesAsync(ct);

var result = await pendingStore.WaitForCompletionAsync(
    pending.Id,
    TimeSpan.FromSeconds(60),
    cancellationToken);
```

### Тесты (фаза 2 MVP)

| Тест | Файл |
|------|------|
| Stage → complete | `tests/IntegrationFlow.EntityFrameworkCore.Tests/EfRpcPendingStoreTests.cs` |
| `WaitForCompletionAsync` | там же |

**DoD фазы 2 (полный):** не выполнен — E2E AsyncOutbox, runbook, SKIP LOCKED для PostgreSQL в backlog.

---

## 3. Backlog (фаза 2–3)

| # | Задача | Приоритет |
|---|--------|-----------|
| 1 | E2E integration test AsyncOutbox (Docker) | P1 |
| 2 | Runbook abandoned pending replay | P1 |
| 3 | PostgreSQL SKIP LOCKED claim для RpcPending | P2 |
| 4 | `CreateSentAndWaitAsyncOutboxIntegration` — high-level API | P2 |
| 5 | Metrics: `rpc.pending.count`, `rpc.pending.duration` | P2 |
| 6 | Фаза 3: `IRpcCompensationHandler`, cleanup jobs | P3 |

---

## 4. Семантика по режимам

| Режим | Critical TX | Timeout |
|-------|-------------|---------|
| **Sync RPC** + MessageId + response cache | ❌ | Retry-safe при idempotent server |
| **AsyncOutbox** + EF pending | ✅ | `WaitForCompletionAsync` → `TimedOut` |

---

## 5. Связь с рисками R1/R2

| Риск | Фаза 1 | Фаза 2 |
|------|--------|--------|
| Timeout → unknown state (sync) | Mitigated: retry + cache | — |
| Нет outbox для RPC | — | Mitigated: AsyncOutbox + pending TX |
| Critical flow через sync RPC | Governance (runbook) | AsyncOutbox |

Полная стратегия: [`2242-sentandwait-rpc-critical-flows.md`](plans/2026-07-04_2242-sentandwait-rpc-critical-flows.md).

---

## 6. Проверка

```bash
dotnet build IntegrationFlow.sln -c Release
dotnet test IntegrationFlow.sln -c Release --filter "Category!=Integration"
dotnet test IntegrationFlow.sln -c Release --filter "Category=Integration"  # Docker
```

Ожидаемо unit: **117** тестов (90 Core + 11 Metrics + 16 EF).
