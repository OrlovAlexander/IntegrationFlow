# Runbook: replay abandoned outbox messages

**Статус:** актуально  
**Создан:** 2026-07-03 22:16 (UTC+3)

Сообщение outbox переходит в статус `Failed` (abandoned), когда `OutboxRelayService` исчерпал `MaxAttempts` и вызвал `IOutboxStore.MarkAbandonedAsync`. Такие сообщения **не публикуются автоматически** — требуется ручной replay после устранения причины сбоя.

---

## 1. Обнаружение

Мониторинг через `IIntegrationFlowMetrics`:

- `RecordOutboxRelayAbandoned(count)` — счётчик abandoned
- `RecordOutboxPending(count)` — backlog pending

SQL-диагностика (EF store):

```sql
SELECT "Id", "ProfileName", "AttemptCount", "LastError", "CreatedAt"
FROM "OutboxMessages"
WHERE "Status" = 3  -- Failed (Pending=0, InFlight=1, Published=2, Failed=3)
ORDER BY "CreatedAt";
```

> Числовое значение `Status` зависит от enum в приложении; для IntegrationFlow EF entity `Failed = 2`.

---

## 2. Устранение причины

Перед replay проверьте:

1. **Broker доступен** — RabbitMQ принимает соединения, очередь/exchange существуют
2. **Topology корректна** — routing key, mandatory, publisher confirms
3. **Payload валиден** — сериализация, размер, content-type
4. **Credentials / TLS** — секреты актуальны

Replay без исправления причины приведёт к повторному abandoned.

---

## 3. Replay одного сообщения

Через `IOutboxStore.ReplayAbandonedAsync`:

```csharp
var replayed = await outboxStore.ReplayAbandonedAsync(
    id: abandonedMessageId,
    resetAttemptCount: false,  // true — сбросить счётчик попыток
    cancellationToken);

if (!replayed)
{
    // Сообщение не найдено или статус != Failed
}
```

| Параметр | Поведение |
|----------|-----------|
| `resetAttemptCount: false` | Статус → `Pending`, `AttemptCount` сохраняется |
| `resetAttemptCount: true` | Статус → `Pending`, `AttemptCount = 0` |

После replay `OutboxRelayService` подхватит сообщение при следующем цикле relay.

---

## 4. Массовый replay (ops script)

Пример для EF Core:

```csharp
await using var db = await dbContextFactory.CreateDbContextAsync(ct);
var abandonedIds = await db.OutboxMessages
    .Where(message => message.Status == OutboxMessageStatus.Failed)
    .Select(message => message.Id)
    .ToListAsync(ct);

var store = new EfOutboxStore<MyDbContext>(dbContextFactory);
foreach (var id in abandonedIds)
{
    var replayed = await store.ReplayAbandonedAsync(id, resetAttemptCount: true, ct);
    logger.LogInformation("Replay {Id}: {Replayed}", id, replayed);
}
```

> Для production предпочтительнее replay **поштучно** после анализа `LastError`, а не массовый сброс.

---

## 5. Проверка после replay

1. Метрика `RecordOutboxRelayPublished` увеличилась для профиля
2. Статус сообщения в БД → `Published`
3. Сообщение появилось в целевой очереди RabbitMQ
4. Consumer обработал (при наличии dedup — идемпотентно по `MessageId = OutboxId`)

---

## 6. Связанные компоненты

| Компонент | Путь |
|-----------|------|
| `IOutboxStore.ReplayAbandonedAsync` | `src/IntegrationFlow.Core/.../Outbox/IOutboxStore.cs` |
| `EfOutboxStore` | `src/IntegrationFlow.EntityFrameworkCore/Outbox/EfOutboxStore.cs` |
| `OutboxRelayService` | `src/IntegrationFlow.Core/.../Outbox/OutboxRelayService.cs` |
| Transactional enqueue | [`docs/examples/2026-07-03_1639-ef-outbox-transaction.md`](../examples/2026-07-03_1639-ef-outbox-transaction.md) |
