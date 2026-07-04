# Полный анализ IntegrationFlow и оценка рисков

**Статус:** актуально  
**Создан:** 2026-07-04 22:34 (UTC+3)  
**Обновлён:** 2026-07-04 22:34 (UTC+3)  
**Связанные документы:** [`2026-07-04_2128-integrationflow-full-analysis.md`](2026-07-04_2128-integrationflow-full-analysis.md) (superseded, v10), [`plans/2026-07-04_2130-remaining-risks-mitigation.md`](plans/2026-07-04_2130-remaining-risks-mitigation.md) (код/docs выполнены; NuGet publish — ops)

Актуальное состояние после коммита `a9a5e92` (RPC metrics, pool reuse, runbooks). Локально **131 тест** (87 + 10 + 13 + 2 + 19) — все зелёные в Release, CI на GitHub Actions (unit → integration → pack).

---

## 1. Что это за решение

**IntegrationFlow** — .NET-библиотека для построения интеграций между системами. Три NuGet-пакета:

| Пакет | TFM | Назначение |
|-------|-----|------------|
| `IntegrationFlow.Core` | net8.0 + netstandard2.0 | Каркас, RabbitMQ (3 сценария), outbox, dedup |
| `IntegrationFlow.EntityFrameworkCore` | net8.0 | Production stores (outbox claim + dedup) |
| `IntegrationFlow.Metrics.OpenTelemetry` | net8.0 | Метрики через `System.Diagnostics.Metrics` |

### Зрелость по сценариям

| Паттерн | Статус | Production-ready |
|---------|--------|------------------|
| **ReceiveAndProcess** (consumer) | Высокая | Да, при правильном adoption |
| **SentAndForgot** (producer + outbox) | Высокая | Да, с outbox + EF |
| **SentAndWait** (RabbitMQ RPC) | Средняя–высокая (MVP + async) | Условно — не для critical TX |
| **SentAndWait** (REST sample) | Низкая | Нет — без гарантий |

### Структура

```
IntegrationFlow/
├── src/IntegrationFlow.Core/
│   └── 00InnerUsage/RabbitMq/
│       ├── ReceiveAndProcess/     # consumer
│       ├── SentAndForgot/         # publisher + outbox relay
│       └── SentAndWait/           # request-reply RPC (sync + async)
├── src/IntegrationFlow.EntityFrameworkCore/
├── src/IntegrationFlow.Metrics.OpenTelemetry/
└── tests/                         # 131 тест (unit + integration)
    ├── IntegrationFlow.Core.Tests                 (87)
    ├── IntegrationFlow.Metrics.OpenTelemetry.Tests (10)
    ├── IntegrationFlow.EntityFrameworkCore.Tests  (13)
    ├── IntegrationFlow.Core.IntegrationTests        (19)
    └── IntegrationFlow.EntityFrameworkCore.IntegrationTests (2)
```

---

## 2. Архитектура

```mermaid
flowchart TB
    subgraph producer [Producer — SentAndForgot]
        TX[Business TX] --> Stage[IOutboxEnqueue.Stage]
        Stage --> Save[SaveChanges]
        Save --> Relay[OutboxRelayService]
        Relay --> Claim[ClaimPending SKIP LOCKED]
        Claim --> Pub["Publish MessageId = OutboxId"]
    end

    subgraph rpc [SentAndWait — RabbitMQ RPC]
        Client[RabbitMqRequestReplyTransmitter] -->|ReplyTo + CorrelationId| ReqQ[Request queue]
        ReqQ --> Server[ReceiveAndProcess handler]
        Server --> Reply[RabbitMqReplyPublisher]
        Reply -->|CorrelationId| Client
    end

    subgraph consumer [Consumer — ReceiveAndProcess]
        Host[IHost / legacy BeginReceiving] --> Worker[RabbitMqListenerWorker]
        Worker --> Dedup{IMessageDeduplicationStore?}
        Dedup -->|Duplicate| Skip[Skip + Ack]
        Dedup -->|New| Process[Handler]
        Process -->|OK| Mark[MarkProcessed]
        Process -->|Fail| Release[ReleaseProcessing]
    end
```

### Семантика доставки

| Сценарий | Гарантия | Комментарий |
|----------|----------|-------------|
| ReceiveAndProcess | **At-least-once** | Ack после обработки, dedup |
| SentAndForgot + outbox | **At-least-once** | Outbox TX + relay + confirms |
| SentAndWait RPC | **At-most-once** | Timeout = unknown state; outbox не поддерживается |

Ключевые компоненты:

- [`OutboxRelayService`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/Outbox/OutboxRelayService.cs)
- [`RabbitMqListenerWorker`](../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/ReceiveAndProcess/Workers/RabbitMqListenerWorker.cs)
- [`RabbitMqRequestReplyTransmitter`](../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/SentAndWait/Transmitters/RabbitMqRequestReplyTransmitter.cs)
- [`RabbitMqRequestReplyConnection`](../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/SentAndWait/Connections/RabbitMqRequestReplyConnection.cs)
- [`RabbitMqRequestReplyConnectionPool`](../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/SentAndWait/Connections/RabbitMqRequestReplyConnectionPool.cs)
- [`RabbitMqReplyPublisher`](../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/SentAndWait/Reply/RabbitMqReplyPublisher.cs)
- [`RabbitMqReplyPublisherPool`](../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/SentAndWait/Reply/RabbitMqReplyPublisherPool.cs)
- [`SentAndWaitIntegration`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/SentAndWait/SentAndWaitIntegration.cs)
- [`EfOutboxStore`](../src/IntegrationFlow.EntityFrameworkCore/Outbox/EfOutboxStore.cs)
- [`OpenTelemetryIntegrationFlowMetrics`](../src/IntegrationFlow.Metrics.OpenTelemetry/OpenTelemetryIntegrationFlowMetrics.cs)

Outbox relay использует `MessageId = OutboxId` для идемпотентности consumer при сбое `MarkPublished`:

```122:126:src/IntegrationFlow.Core/Contexts/Integrations/03Domain/Outbox/OutboxRelayService.cs
                        var transmitData = new TransmitData(message.Payload, message.Id.ToString("N"))
                            .WithCorrelationId(message.Id.ToString("N"));

                        transmitter!.TransmitWithResult(transmitData);
                        await outboxStore.MarkPublishedAsync(message.Id, workerId, cancellationToken).ConfigureAwait(false);
```

---

## 3. Что сделано хорошо

### Consumer (ReceiveAndProcess)

- Ack **после** завершения обработки (включая async path)
- Dedup с `ReleaseProcessingAsync`, lock expiry, `InProgress` → nack requeue
- `ReceiveAndProcessHostedService` + graceful shutdown
- Overload без handler удалён; legacy NoOp → nack

### Producer (SentAndForgot)

- Publisher confirms, `IntegrateWithResult()`, `MessageId = OutboxId`
- Transactional outbox + EF SKIP LOCKED claim
- `ReplayAbandonedAsync` + runbook

### SentAndWait RabbitMQ (после async + P3)

- `IntegrateAsync()` / `IntegrateWithResultAsync()` с `CancellationToken`
- `IntegrateWithResult()` — typed result вместо только callback
- Concurrent RPC: `MaxConcurrentRequests` (default `1`, `0` = без лимита)
- Connection reuse: `ReuseConnection` + internal pool per profile с health check / eviction
- `ReuseReplyConnection` (default `true`) + `RabbitMqReplyPublisherPool` на server-side
- `DirectReplyTo` (default) + `ExclusiveQueue` fallback
- `RabbitMqReplyPublisher` — server-side reply на `ReplyTo`
- RPC metrics: `RecordRequestReply` в transmitter (`finally`)
- Unit + E2E тесты (roundtrip, timeout, parallel requests)

### Observability и distribution

- `IntegrationFlow.Metrics.OpenTelemetry` — consumer/outbox/RPC metrics + runbook алертов
- CI: unit → integration → pack
- `release.yml` с integration gate перед pack (нужен `NUGET_API_KEY` для publish)
- Runbooks: production adoption, RPC adoption, metrics, NuGet release

### Тесты и CI

- **131 тест**, E2E critical path + legacy + RPC roundtrip + parallel async

---

## 4. Матрица рисков

### Высокий приоритет — фундаментальные (inherent)

| # | Риск | Сценарий | Mitigation |
|---|------|----------|------------|
| 1 | Publish OK → MarkPublished fail | SentAndForgot | `MessageId = OutboxId`; идемпотентный consumer |
| 2 | MarkProcessed fail после успешной обработки | ReceiveAndProcess | Идемпотентные handlers |
| 3 | Direct publish без outbox | SentAndForgot | `IOutboxEnqueue` в той же TX |
| 4 | Dedup без MessageId | ReceiveAndProcess | Всегда задавать `MessageId` |
| **R1** | **Timeout после publish, server обработал** | **SentAndWait** | Идемпотентный server; retry с новым CorrelationId |
| **R2** | **Reply publish fail после обработки** | **SentAndWait** | Client timeout + retry; server dedup по MessageId |

Эти риски **не устраняются каркасом полностью** — требуют правильного adoption.

### Высокий приоритет — adoption / API

| # | Риск | Статус | Комментарий |
|---|------|--------|-------------|
| **A** | NoOp handler — ack без обработки | **Закрыт (hosted)** | Overload без handler удалён |
| **A′** | Legacy default NoOp processor | **Закрыт** | `GetInboxMessageProcessing` → null → nack |
| **B** | Ограниченный public API | **Закрыт** | RPC: public `RabbitMqReplyPublisher` |
| **R3** | Exception глотается без `ThrowOnFailure` | **Открыт (adoption)** | Есть `IntegrateWithResult()`, но default `ThrowOnFailure=false` |
| **R4** | Server ack до reply | **Открыт (adoption)** | Handler должен reply **до** return из Process |

### Средний приоритет

| # | Риск | Статус | Кратко |
|---|------|--------|--------|
| 5 | Prefetch > 1 + concurrent handler | Открыт | Race в non-thread-safe handlers |
| 6 | Hosted worker только net8.0 | Открыт | netstandard2.0 — manual paths |
| 7 | EF dedup — отдельный DbContext | By design | Не атомарен с business TX |
| 8 | InMemory stores в samples | Открыт | Copy-paste в prod → потеря данных |
| 9 | Mandatory timeout 100ms | Открыт | SentAndForgot topology misconfig |
| 10 | Abandoned outbox | **Закрыт (ops)** | `ReplayAbandonedAsync` + runbook |
| **R5** | Один in-flight RPC на transmitter | **Закрыт** | `MaxConcurrentRequests` + correlation map |
| **R6** | ReplyPublisher — новое соединение на reply | **Закрыт** | `ReuseReplyConnection` + pool (default `true`) |
| **R7** | Нет metrics для RPC | **Закрыт** | `RecordRequestReply` + runbook |
| **R8** | Нет `IntegrateWithResult()` для SentAndWait | **Закрыт** | Реализовано в `eee98f8` |
| **R9** | DirectReplyTo требует RabbitMQ ≥ 3.4 | By design | Fallback: `ExclusiveQueue` |
| **A3** | Sync-over-async в `Integrate()` | Открыт | Runbook; использовать `IntegrateAsync` |
| **A5** | Connection pool без eviction | **Закрыт** | Health check + `ForceDispose` / `Invalidate` в pool |
| 15 | Release без integration tests | **Закрыт** | `release.yml` — integration gate |
| 21 | NuGet не опубликован | Открыт (ops) | Workflow готов; нужен tag + API key |

### Низкий приоритет / техдолг

| # | Риск | Статус |
|---|------|--------|
| 16 | Sync-over-async в legacy paths | Остаётся |
| 17 | DLQ не создаётся runtime | Осознанно — ops responsibility |
| 18 | REST SentAndWait sample | Legacy, без гарантий |
| 19 | `IntegrationScheduler` | Dead code |
| 20 | Нет distributed tracing | Только metrics hooks |
| **R10** | ExclusiveQueue leak при crash | Mitigated — `DirectReplyTo` default |

### Безопасность

| # | Риск | Описание | Статус |
|---|------|----------|--------|
| S1 | Credentials в `rabbitmq.json` | Plain JSON — нужен secrets manager | Частично — README + env vars |
| S2 | Нет TLS samples | AMQPS не продемонстрирован | Частично — AMQPS sample в README |
| S3 | Guest credentials по умолчанию | Ок для dev, опасно для prod | By design для dev |

---

## 5. Оценка по критериям

| Критерий | Оценка | Комментарий |
|----------|--------|-------------|
| **Архитектура** | **8/10** | Outbox, dedup, async RPC; RPC без outbox — by design |
| **Delivery guarantees (async paths)** | **~98%** | ReceiveAndProcess + SentAndForgot зрелые |
| **Production readiness (ReceiveAndProcess)** | **Да, при adoption** | Outbox TX + EF + dedup + DLQ |
| **Production readiness (SentAndForgot)** | **Да, при adoption** | Outbox + EF + confirms |
| **Production readiness (SentAndWait RPC)** | **Условно** | Async API + runbooks; не для critical TX |
| **Public API / ergonomics** | **8/10** | Async + `IntegrateWithResult`; RPC metrics |
| **Тестовое покрытие** | **8/10** | 131 тест, E2E + parallel RPC |
| **Документация** | **8/10** | README, plans, runbooks, analysis v11 |
| **Observability** | **9/10** | Metrics consumer/outbox/RPC; runbook алертов |
| **Распространение** | **7/10** | CI pack работает; NuGet publish не выполнен |

---

## 6. Обязательные условия для production

### ReceiveAndProcess + SentAndForgot

1. **Outbox через `IOutboxEnqueue`** в той же TX, что business-данные
2. **EF stores** — не InMemory
3. **Идемпотентные handlers** + **`MessageId`** на всех сообщениях
4. **DLQ на брокере** для poison messages
5. **Явный business handler** — hosted или custom processor side
6. **Мониторинг**: `AddIntegrationFlowOpenTelemetryMetrics()` + алерты
7. **Secrets management** — не хранить пароли в plain JSON

Runbook: [`runbooks/2026-07-04_2130-production-adoption.md`](runbooks/2026-07-04_2130-production-adoption.md).

### SentAndWait RPC (дополнительно)

1. **`SentAndWaitIntegrationOptions.ThrowOnFailure = true`** или явная обработка `IntegrateWithResult().TimedOut`
2. **Идемпотентный server handler** — retry клиента после timeout
3. **Reply до return из Process** — иначе ack без ответа
4. **`ResponseTimeoutSeconds`** с запасом под p99 latency
5. В ASP.NET Core — **`IntegrateAsync()`**, не sync `Integrate()`
6. Для нагрузки: `ReuseConnection=true`, `MaxConcurrentRequests` > 1
7. **Не использовать RPC для critical transactional flows** — outbox не поддерживается

Runbook: [`runbooks/2026-07-04_2130-sentandwait-rpc-adoption.md`](runbooks/2026-07-04_2130-sentandwait-rpc-adoption.md).

---

## 7. Рекомендуемые следующие шаги

| P | Задача | Статус |
|---|--------|--------|
| **P3** | Закрытие главных оставшихся рисков (v1.0) | **Код/docs выполнены**; NuGet publish — ops |
| **P3** | RPC metrics (`RecordRequestReply`) | **Закрыт** |
| **P3** | `IntegrateWithResult()` для SentAndWait | **Закрыт** |
| **P3** | Async `IntegrateAsync()` + concurrent RPC | **Закрыт** |
| **P3** | ReplyPublisher connection reuse | **Закрыт** |
| **P3** | Connection pool eviction | **Закрыт** |
| **P3** | Integration tests в `release.yml` | **Закрыт** |
| **P3** | Runbooks adoption | **Закрыт** |
| **P3** | NuGet publish (`NUGET_API_KEY` + tag) | **Открыт — ops** |
| **P3** | Distributed tracing | **Открыт — optional** |
| **P4** | SentAndWait RPC critical flows (R1/R2) | **В работе** — фаза 1 ✅, фаза 2 MVP ✅ — [`plans/2026-07-04_2242-sentandwait-rpc-critical-flows.md`](plans/2026-07-04_2242-sentandwait-rpc-critical-flows.md), [`2026-07-04_2301-sentandwait-rpc-implementation-status.md`](2026-07-04_2301-sentandwait-rpc-implementation-status.md) |

План: [`plans/2026-07-04_2130-remaining-risks-mitigation.md`](plans/2026-07-04_2130-remaining-risks-mitigation.md).

---

## 8. Итоговый вывод

Проект **архитектурно зрелый** для RabbitMQ-интеграций. После `a9a5e92` закрыты технические gaps P3: RPC metrics, pool reuse (client + server), runbooks, integration gate в release.

**Blockers сняты** на уровне базовой функциональности для всех трёх RabbitMQ-сценариев. Единственный ops-blocker до v1.0 — **публикация на NuGet** (`git tag v1.0.0 && git push origin v1.0.0` + `NUGET_API_KEY`).

### Главные оставшиеся риски

| Категория | Главный риск | Severity |
|-----------|--------------|----------|
| **SentAndWait RPC** | At-most-once при timeout; нет outbox | Высокий для critical flows |
| **Adoption** | `ThrowOnFailure=false` по умолчанию; server должен reply до ack | Высокий |
| **Operations** | NuGet не опубликован | Средний |
| **Async adoption** | Sync `Integrate()` в ASP.NET → thread pool starvation | Средний |
| **Security** | Plain credentials в dev defaults | Средний (при правильном adoption — низкий) |

### Рекомендации по выбору паттерна

| Задача | Паттерн |
|--------|---------|
| Critical async flows (заказы, платежи) | **SentAndForgot + outbox + EF** |
| Sync query/command, read-only | **SentAndWait RPC** + идемпотентный server + `IntegrateAsync` |
| Fire-and-forget без TX | SentAndForgot direct publish (осознанный trade-off) |

Direct publish без outbox, отсутствие `MessageId` и RPC без идемпотентного server — **осознанные anti-patterns**, не дефекты каркаса.
