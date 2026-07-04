# Runbook: production adoption checklist

**Статус:** актуально  
**Создан:** 2026-07-04 21:30 (UTC+3)

Чеклист для безопасного внедрения IntegrationFlow в production (ReceiveAndProcess + SentAndForgot).

---

## Обязательный чеклист

- [ ] **Outbox через `IOutboxEnqueue`** в той же TX, что business-данные (не direct publish после commit)
- [ ] **EF stores** (`AddIntegrationFlowEfOutbox`, `AddIntegrationFlowEfDeduplication`) — не InMemory из samples
- [ ] **`MessageId` на всех сообщениях** — иначе dedup не работает (риск #4)
- [ ] **Идемпотентные handlers** — at-least-once доставка
- [ ] **DLQ на брокере** для poison messages (`x-dead-letter-exchange`)
- [ ] **Явный business handler** — `AddIntegrationFlowRabbitMqListener` или custom processor side
- [ ] **`AddIntegrationFlowOpenTelemetryMetrics()`** + алерты
- [ ] **Secrets** — не хранить пароли в plain `rabbitmq.json` (env vars / secrets manager)
- [ ] **`ThrowOnFailure = true`** в production (SentAndForgot и SentAndWait)

---

## Producer (SentAndForgot)

```csharp
services.AddDbContext<MyDbContext>(...);
services.AddDbContextFactory<MyDbContext>(...);
services.AddIntegrationFlowEfOutbox<MyDbContext>();
services.AddIntegrationFlowOutboxRelay(options => { ... });

SentAndForgotIntegrationOptions.ThrowOnFailure = true;
```

Enqueue в той же транзакции:

```csharp
await using var tx = await dbContext.Database.BeginTransactionAsync();
dbContext.EnqueueOutboxMessage(profileName, payload);
await dbContext.SaveChangesAsync();
await tx.CommitAsync();
```

Подробнее: [`examples/2026-07-03_1639-ef-outbox-transaction.md`](../examples/2026-07-03_1639-ef-outbox-transaction.md).

---

## Consumer (ReceiveAndProcess)

```csharp
services.AddIntegrationFlow();
services.AddIntegrationFlowOpenTelemetryMetrics();
services.AddIntegrationFlowRabbitMqListener("Inbox", handler);
services.AddIntegrationFlowEfDeduplication<MyDbContext>();
```

| Параметр | Рекомендация |
|----------|--------------|
| `PrefetchCount` | `1` для non-thread-safe handlers |
| `RequeueOnFailure` | `false` + DLQ на брокере |
| `MaxRetryCount` | Настроить лимит → DLQ |

---

## Inherent риски (не устраняются каркасом)

| Риск | Mitigation |
|------|------------|
| Publish OK → MarkPublished fail | `MessageId = OutboxId`; идемпотентный consumer |
| MarkProcessed fail после обработки | Идемпотентный handler |
| Direct publish без outbox | `IOutboxEnqueue` в TX |
| Dedup без MessageId | Всегда задавать `MessageId` |

---

## Operations

| Ситуация | Действие |
|----------|----------|
| Abandoned outbox | [`2026-07-03_2216-abandoned-outbox-replay.md`](2026-07-03_2216-abandoned-outbox-replay.md) |
| Высокий outbox backlog | Проверить relay worker, broker, DB locks |
| Consumer failures | Логи handler, DLQ, poison messages |

---

## SentAndWait RPC

Отдельный runbook: [`2026-07-04_2130-sentandwait-rpc-adoption.md`](2026-07-04_2130-sentandwait-rpc-adoption.md).

---

## Связанные документы

- [`plans/2026-07-03_1639-production-readiness.md`](../plans/2026-07-03_1639-production-readiness.md)
- [`2026-07-04_2128-integrationflow-full-analysis.md`](../2026-07-04_2128-integrationflow-full-analysis.md)
