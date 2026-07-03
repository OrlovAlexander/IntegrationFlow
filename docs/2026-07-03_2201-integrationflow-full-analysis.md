# Полный анализ IntegrationFlow и оценка рисков

**Статус:** актуально  
**Создан:** 2026-07-03 22:01 (UTC+3)  
**Обновлён:** 2026-07-03 22:16 (UTC+3)  
**Связанные документы:** [`2026-07-03_2119-integrationflow-full-analysis.md`](2026-07-03_2119-integrationflow-full-analysis.md) (superseded)

Актуальное состояние после коммита `88766ec` (закрытие P0–P2: public API, metrics, NuGet и legacy E2E) и доработок P1/P2 (NoOp legacy processor, abandoned outbox replay). **103 теста зелёные**, CI на GitHub Actions, `dotnet pack` собирает пакеты `IntegrationFlow.Core` и `IntegrationFlow.EntityFrameworkCore` версии `1.0.0`.

---

## 1. Что это за проект

**IntegrationFlow** — .NET-библиотека для построения интеграций между системами. Два NuGet-пакета:

| Пакет | TFM | Назначение |
|-------|-----|------------|
| `IntegrationFlow.Core` | net8.0 + netstandard2.0 | Каркас, RabbitMQ, outbox, dedup |
| `IntegrationFlow.EntityFrameworkCore` | net8.0 | Production stores (outbox claim + dedup) |

### Сценарии интеграции

| Паттерн | Зрелость | Транспорт | Production-ready |
|---------|----------|-----------|------------------|
| **ReceiveAndProcess** | Высокая | RabbitMQ consumer | Да, при правильном adoption |
| **SentAndForgot** | Высокая | RabbitMQ + transactional outbox | Да, с outbox + EF |
| **SentAndWait** | Низкая | REST sample | Нет |

### Структура

```
IntegrationFlow/
├── src/IntegrationFlow.Core/                 # Домен, RabbitMQ, outbox, dedup
├── src/IntegrationFlow.EntityFrameworkCore/  # EF stores, SKIP LOCKED claim
├── Directory.Build.props                     # NuGet metadata, Version 1.0.0
└── tests/                                    # 94 теста (unit + integration)
    ├── IntegrationFlow.Core.Tests                 (75)
    ├── IntegrationFlow.Core.IntegrationTests        (14)
    ├── IntegrationFlow.EntityFrameworkCore.Tests    (13)
    └── IntegrationFlow.EntityFrameworkCore.IntegrationTests (2)
```

---

## 2. Архитектура гарантий доставки

```mermaid
flowchart TB
    subgraph producer [Producer — SentAndForgot]
        TX[Business TX] --> Stage[IOutboxEnqueue.Stage]
        Stage --> Save[SaveChanges]
        Save --> Relay[OutboxRelayService]
        Relay --> Claim[ClaimPending SKIP LOCKED]
        Claim --> Pub[Publish MessageId=OutboxId]
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

**Семантика — at-least-once**, не exactly-once. Это корректно и задокументировано.

Ключевые компоненты:

- [`OutboxRelayService`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/Outbox/OutboxRelayService.cs)
- [`RabbitMqListenerWorker`](../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/ReceiveAndProcess/Workers/RabbitMqListenerWorker.cs)
- [`ReceiveAndProcessHostedService`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/ReceiveAndProcess/ReceiveAndProcessHostedService.cs)
- [`IIntegrationFlowMetrics`](../src/IntegrationFlow.Core/Contexts/Integrations/03Domain/Metrics/IIntegrationFlowMetrics.cs)
- [`EfOutboxStore`](../src/IntegrationFlow.EntityFrameworkCore/Outbox/EfOutboxStore.cs)
- [`EfMessageDeduplicationStore`](../src/IntegrationFlow.EntityFrameworkCore/Deduplication/EfMessageDeduplicationStore.cs)

---

## 3. Что сделано хорошо

### Consumer (ReceiveAndProcess)

- Ack **после** обработки (async-баг закрыт)
- Dedup с `ReleaseProcessingAsync`, lock expiry, `InProgress` → nack requeue
- **`RabbitMqListenerWorker`** — единый async loop для legacy и hosted path
- **`ReceiveAndProcessHostedService`** + `AddIntegrationFlowRabbitMqListener()` — graceful shutdown через `IHost`
- Убран `Thread` / `Thread.Abort` (техдолг #17 закрыт)
- Manual ack, prefetch, requeue policy, MaxRetryCount → DLQ
- **`AddIntegrationFlowRabbitMqListener` требует handler** — overload без handler удалён (`88766ec`)

### Producer (SentAndForgot)

- Publisher confirms, `IntegrateWithResult()`, mandatory + BasicReturn fix
- Transactional outbox: `IOutboxEnqueue` в TX приложения, relay с claim/backoff

### EF Core (production)

- Разделение `IOutboxEnqueue` (scoped DbContext) и `IOutboxStore` (factory для relay)
- `EfOutboxStore` с SKIP LOCKED (PostgreSQL) / UPDLOCK (SQL Server)
- `EfMessageDeduplicationStore` с processing lock expiry
- Concurrent claim integration tests на PostgreSQL и SQL Server

### Public API и распространение (`88766ec`)

- Открыты: `PublisherBase`, `RabbitMqPublisher`, `RabbitMqIntegrationPublisherSideBase`, `IntegrationProcessorSideBase`
- NuGet packaging: `PackageId`, `IsPackable`, `Directory.Build.props` с `Version 1.0.0`
- `dotnet pack` собирает оба пакета

### Observability (`88766ec`)

- `IIntegrationFlowMetrics` — hooks для duration обработки, outbox relay published/failed/abandoned/pending
- `NullIntegrationFlowMetrics` по умолчанию; реализация подключается через DI

### Тесты и CI

- E2E: outbox relay, consumer handler, hosted listener, **legacy `BeginReceiving()`**
- Unit: public API extension, metrics wiring
- GitHub Actions: unit + integration (Testcontainers) — [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)

### Документация

- README, plans, risk analysis — хронологический указатель в [`docs/README.md`](README.md)

---

## 4. Матрица рисков

### Высокий приоритет — фундаментальные (inherent at-least-once)

| # | Риск | Последствие | Mitigation |
|---|------|-------------|------------|
| 1 | **Publish OK → MarkPublished fail** | Дубликат publish | `MessageId = OutboxId`; идемпотентный consumer |
| 2 | **MarkProcessed fail после успешной обработки** | Повтор business logic | Идемпотентные handlers |
| 3 | **Direct publish без outbox** | Потеря после DB commit | `IOutboxEnqueue` в той же TX |
| 4 | **Dedup без MessageId** | Dedup пропускается (warn в лог) | Всегда задавать `MessageId` |

Эти риски **не устраняются каркасом** — требуют правильного adoption на стороне приложения.

### Высокий приоритет — adoption / API

| # | Риск | Статус | Комментарий |
|---|------|--------|-------------|
| **A** | **NoOp handler — сообщения ack без обработки** | **Закрыт (hosted)** | Overload без handler **удалён** (`88766ec`). Hosted API требует `handleMessage` или `createProcessing` |
| **A′** | **Legacy default NoOp processor** | **Закрыт** | `DefaultRabbitMqIntegrationProcessorSide` возвращает `null` для `GetInboxMessageProcessing` → `ProcessorBase` бросает `NotImplementedException`; сообщение не ack'ится |
| **B** | **Ограниченный public API** | **Закрыт** | `PublisherBase`, `RabbitMqPublisher`, `RabbitMqIntegrationPublisherSideBase`, `IntegrationProcessorSideBase` — public (`88766ec`). Internal остаются `RabbitMqListenerWorker`, `HostedRabbitMqIntegrationProcessorSide` |

### Средний приоритет

| # | Риск | Статус | Кратко |
|---|------|--------|--------|
| 5 | Prefetch > 1 + concurrent consumer | Открыт | Race в non-thread-safe handlers |
| 6 | Outbox/listener hosted worker только net8.0 | Открыт | netstandard2.0 — manual `RunAsync` / `RelayBatchAsync` |
| 7 | EF dedup — отдельный DbContext на операцию | By design | Не атомарен с business TX |
| 8 | InMemory stores в samples | Открыт | Copy-paste в prod → потеря данных |
| 9 | Mandatory timeout 100ms | Открыт | Теоретический silent loss при misconfigured topology |
| 10 | Abandoned outbox | **Закрыт (ops)** | `IOutboxStore.ReplayAbandonedAsync` + runbook [`runbooks/2026-07-03_2216-abandoned-outbox-replay.md`](runbooks/2026-07-03_2216-abandoned-outbox-replay.md) |
| 11 | ~~Legacy `BeginReceiving()` без E2E~~ | **Закрыт** | `RabbitMqListenerLegacyEndToEndTests` (`88766ec`) |
| 12 | SQLite vs PG/SQL claim | Открыт | Unit-тесты на SQLite; prod SQL — только integration |
| 13 | Custom processor side через hosted API | Открыт | Только через factory `createProcessing` / `createDeduplicationStore` |
| 14 | EF dedup DI (Scoped) vs background listener | Открыт | Dedup wire через `createDeduplicationStore` в DI |

Подробная детализация рисков 5–14 — в [`2026-07-03_2010-delivery-guarantee-full-analysis.md`](2026-07-03_2010-delivery-guarantee-full-analysis.md), раздел 4.

### Низкий приоритет / техдолг

| # | Риск | Статус |
|---|------|--------|
| 15 | Sync-over-async в legacy paths | Остаётся (`ProcessorBase.Process`, `OutboxTransmitter`, `InMemoryOutboxStore`, `ListenerBase.Stop`) |
| 16 | DLQ не создаётся runtime | Осознанно — ops responsibility |
| 17 | ~~Listener на Thread~~ | **Закрыто** (`7a0bf77`) |
| 18 | `SentAndWait` неполный | REST sample, без delivery guarantees |
| 19 | `IntegrationScheduler` | Dead code (internal, не используется) |
| 20 | ~~Нет metrics/tracing~~ | **Частично закрыто** | `IIntegrationFlowMetrics` есть; готовой реализации (Prometheus/OpenTelemetry) нет |
| 21 | ~~Нет NuGet packaging~~ | **Закрыто** | `PackageId`, `Version 1.0.0`, `dotnet pack` работает; CI publish pipeline нет |

### Безопасность

| # | Риск | Описание |
|---|------|----------|
| S1 | Credentials в `rabbitmq.json` | Пароли в plain JSON рядом с приложением — нужен secrets manager / env vars |
| S2 | Нет TLS-конфигурации в samples | AMQPS не продемонстрирован |
| S3 | Guest credentials по умолчанию | Ок для dev, опасно для prod |

---

## 5. Оценка по критериям

| Критерий | Оценка | Комментарий |
|----------|--------|-------------|
| **Архитектура** | **8/10** | Правильные паттерны (outbox, dedup, confirms), разделение Core/EF |
| **Delivery guarantees** | **~98%** | Критические баги закрыты; inherent at-least-once остаётся |
| **Production readiness (RabbitMQ path)** | **Да, при правильном adoption** | Outbox TX + EF stores + идемпотентные handlers + DLQ |
| **Public API / ergonomics** | **7/10** | Базовые классы открыты; legacy path требует знания архитектуры |
| **Тестовое покрытие** | **8/10** | 103 теста, E2E critical path + legacy, CI |
| **Документация** | **8/10** | Актуализируется по мере изменений |
| **Observability** | **5/10** | Hooks есть (`IIntegrationFlowMetrics`); готовой реализации нет |
| **Распространение** | **6/10** | NuGet packaging есть; CI publish нет |
| **SentAndWait** | **2/10** | Out of scope |

---

## 6. Обязательные условия для production

1. **Outbox через `IOutboxEnqueue`** в той же TX, что business-данные
2. **EF stores** — не InMemory
3. **Идемпотентные handlers** + **`MessageId`** на всех сообщениях
4. **DLQ на брокере** для poison messages
5. **Явный business handler** — hosted: `AddIntegrationFlowRabbitMqListener`; legacy: свой `IntegrationProcessorSideBase`, не default NoOp
6. **Мониторинг**: outbox pending/abandoned, consumer unacked count, relay failures (через `IIntegrationFlowMetrics`)
7. **Secrets management** — не хранить пароли RabbitMQ в plain JSON

---

## 7. Рекомендуемые следующие шаги

| P | Задача | Статус |
|---|--------|--------|
| **P1** | Убрать NoOp из `DefaultRabbitMqIntegrationProcessorSide` (throw вместо silent ack) | ✅ |
| **P1** | Reference implementation `IIntegrationFlowMetrics` (Prometheus/OpenTelemetry) | Открыт |
| **P2** | CI job `dotnet pack` + publish to NuGet.org | Открыт |
| **P2** | Runbook для abandoned outbox replay | ✅ [`runbooks/2026-07-03_2216-abandoned-outbox-replay.md`](runbooks/2026-07-03_2216-abandoned-outbox-replay.md) |
| **P3** | `SentAndWait` или явно пометить out-of-scope | Открыт |

---

## 8. Итоговый вывод

Проект **архитектурно зрелый** для RabbitMQ at-least-once интеграций. За последние итерации закрыты критические баги (async ack, dedup-on-failure, outbox claim, listener Thread) и P0–P2 gaps: NoOp hosted overload, public base classes, metrics hooks, NuGet packaging, legacy E2E.

**Blockers для production сняты** на уровне delivery semantics. Главные **оставшиеся риски — adoption и operations**, не корректность каркаса:

| Категория | Главный риск |
|-----------|--------------|
| Adoption | Legacy path требует явного `IntegrationProcessorSideBase`; идемпотентность и MessageId — на стороне приложения |
| Operations | Нет готовых метрик/dashboards; abandoned outbox replay — runbook + `ReplayAbandonedAsync` |
| Distribution | NuGet собирается, но нет автопубликации в CI |
| Security | Credentials в JSON, нет TLS samples |

Direct publish без outbox и отсутствие `MessageId` — **осознанные anti-patterns**, не дефекты библиотеки.

Для production-критичных интеграций: **outbox + EF + идемпотентные handlers + custom processor wiring + DLQ + мониторинг abandoned outbox**.
