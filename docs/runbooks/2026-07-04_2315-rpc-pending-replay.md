# Runbook: replay abandoned async RPC pending requests

**Статус:** актуально  
**Создан:** 2026-07-04 23:15 (UTC+3)

Запись `RpcPendingRequest` переходит в статус `Failed` (abandoned), когда `RpcPendingRelayService` исчерпал `MaxAttempts` и вызвал `IRpcPendingStore.MarkAbandonedAsync`. Такие запросы **не публикуются автоматически** — требуется ручной replay после устранения причины сбоя (broker, topology, credentials, payload).

> Для запросов в статусе `AwaitingResponse` без ответа см. также `PendingTimeoutSeconds` и `WaitForCompletionAsync` — они переводят запись в `TimedOut`, а не в `Failed`.

---

## 1. Обнаружение

Мониторинг через `IIntegrationFlowMetrics`:

- `RecordRpcPendingRelayAbandoned(count)` — счётчик abandoned после relay
- `RecordRpcPendingAwaiting(count)` — backlog awaiting response
- `RecordRpcPendingCompleted(profile, duration, success, timedOut)` — завершённые round-trip

OpenTelemetry meter names:

| Метрика | Имя |
|---------|-----|
| Relay published | `integrationflow.rpc.pending.relay.published` |
| Relay failed | `integrationflow.rpc.pending.relay.failed` |
| Relay abandoned | `integrationflow.rpc.pending.relay.abandoned` |
| Awaiting gauge | `integrationflow.rpc.pending.awaiting` |
| Completed | `integrationflow.rpc.pending.completed` |
| Duration | `integrationflow.rpc.pending.duration` |

SQL-диагностика (EF store):

```sql
SELECT "Id", "ProfileName", "AttemptCount", "LastError", "CreatedAt", "Status"
FROM "RpcPendingRequests"
WHERE "Status" = 4  -- Failed (Pending=0, InFlight=1, AwaitingResponse=2, Completed=3, Failed=4, TimedOut=5)
ORDER BY "CreatedAt";
```

> Числовые значения `Status` зависят от enum в приложении; сверяйте с `RpcPendingStatus` в коде.

---

## 2. Устранение причины

Перед replay проверьте:

1. **Broker доступен** — RabbitMQ принимает соединения
2. **Request queue / response queue существуют** — для профиля `RequestMode=AsyncOutbox` задан `ResponseQueueName`
3. **RPC server отвечает** — consumer request queue публикует reply в response queue с `CorrelationId = pendingId`
4. **Response correlation worker запущен** — `AddIntegrationFlowRabbitMqRpcResponseCorrelation()`
5. **Payload и content-type валидны**

Replay без исправления причины приведёт к повторному abandoned.

---

## 3. Replay одного запроса

Через `IRpcPendingStore.ReplayAbandonedAsync`:

```csharp
var replayed = await rpcPendingStore.ReplayAbandonedAsync(
    id: abandonedPendingId,
    resetAttemptCount: false,  // true — сбросить счётчик попыток
    cancellationToken);

if (!replayed)
{
    // Запись не найдена или статус != Failed
}
```

| Параметр | Поведение |
|----------|-----------|
| `resetAttemptCount: false` | Статус → `Pending`, `AttemptCount` сохраняется |
| `resetAttemptCount: true` | Статус → `Pending`, `AttemptCount = 0` |

После replay `RpcPendingRelayService` подхватит запись при следующем цикле relay.

---

## 4. Массовый replay (ops script)

Пример для EF Core:

```csharp
await using var db = await dbContextFactory.CreateDbContextAsync(ct);
var abandonedIds = await db.RpcPendingRequests
    .Where(request => request.Status == RpcPendingStatus.Failed)
    .Select(request => request.Id)
    .ToListAsync(ct);

var store = new EfRpcPendingStore<MyDbContext>(dbContextFactory);
foreach (var id in abandonedIds)
{
    await store.ReplayAbandonedAsync(id, resetAttemptCount: true, ct);
}
```

---

## 6. Compensation (Failed / TimedOut)

При `Failed` или `TimedOut` можно зарегистрировать `IRpcCompensationHandler`. Встроенный вариант — `OutboxRpcCompensationHandler` (компенсация через SentAndForgot outbox):

```csharp
services.AddIntegrationFlowEfRpcPending<MyDbContext>();
services.AddIntegrationFlowEfOutbox<MyDbContext>();
services.AddIntegrationFlowEfOutboxRpcCompensation<MyDbContext>(options =>
{
    options.OutboxProfileName = "OrdersCompensation";
});
services.AddIntegrationFlowRpcPendingCompensation();
services.AddIntegrationFlowRpcPendingRelay();
```

Worker `RpcPendingCompensationBackgroundService` находит pending без `CompensatedAt`, вызывает handler и выставляет `CompensatedAt`.

---

## 7. Связанные документы

- [SentAndWait RPC implementation status](../2026-07-04_2301-sentandwait-rpc-implementation-status.md)
- [Abandoned outbox replay](./2026-07-03_2216-abandoned-outbox-replay.md)
- [SentAndWait RPC critical flows plan](../plans/2026-07-04_2242-sentandwait-rpc-critical-flows.md)
