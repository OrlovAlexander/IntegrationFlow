# Статус реализации RabbitMQ P2 — resilience (core)

**Статус:** выполнено (core)  
**Создан:** 2026-07-06 16:17 (UTC+3)  
**Обновлён:** 2026-07-06 16:17 (UTC+3)  
**План:** [`plans/2026-07-06_1519-rabbitmq-p2-resilience-hardening.md`](plans/2026-07-06_1519-rabbitmq-p2-resilience-hardening.md)  
**Предшественник:** [`2026-07-06_1456-rabbitmq-g1-g5-implementation-status.md`](2026-07-06_1456-rabbitmq-g1-g5-implementation-status.md) (P1 ✅)  
**Связанные документы:** [`2026-07-04_2352-rabbitmq-full-analysis.md`](2026-07-04_2352-rabbitmq-full-analysis.md), [`2026-07-05_1455-integrationflow-full-analysis.md`](2026-07-05_1455-integrationflow-full-analysis.md)

---

## Итог

P2 **core** (PR-1 … PR-4) **закрыт** в коде, unit-тестах и README. Optional волна 4 (P2-CB, P2-HA, idle eviction) — **отложена**.

| ID | Задача | Статус |
|----|--------|--------|
| P2-F | TLS (AMQPS) для `RabbitMq` + `RabbitMqPublish` | ✅ |
| P2-D | `ReuseConnection` + pool для SentAndForgot direct publish | ✅ |
| P2-RPC-C | Publisher confirms на RPC request publish | ✅ |
| P2-RPC-M | Opt-in `ReplyMandatory` + `BasicReturn` на server reply | ✅ |
| P2-PF | Pool shutdown hook + gauge pool size | ✅ |
| P2-CB | Circuit breaker | ⏸ Optional |
| P2-HA | Cluster / multiple endpoints | ⏸ Optional |
| P2-PF idle eviction | `ConnectionMaxIdleSeconds` / lifetime | ⏸ Optional (v1.1) |

---

## Изменения в коде

| Компонент | Изменение |
|-----------|-----------|
| `RabbitMqConfiguration` / `RabbitMqPublishConfiguration` | P2-F: `SslEnabled`, `SslServerName` |
| `RabbitMqConfigurationLoader` / `RabbitMqPublishConfigurationLoader` | P2-F: `Copy()` SSL fields |
| `RabbitMqPublishConfiguration` | P2-D: `ReuseConnection` |
| `RabbitMqPublishConnection` | P2-D: `ILeaveOpenOnDispose`, `ForceDispose()` |
| `RabbitMqPublishConnectionPool` | P2-D: `GetOrAdd`, `Invalidate`, metrics |
| `RabbitMqSentAndForgotIntegrationOppositeSideBase` | P2-D: pool wire-up |
| `RabbitMqRequestReplyConfiguration` | P2-RPC-C/M: confirms + `ReplyMandatory` |
| `RabbitMqRequestReplyTransmitter` | P2-RPC-C: `RabbitMqPublisherConfirms` после request publish |
| `RabbitMqReplyPublisher` | P2-RPC-M: mandatory reply + `BasicReturn` |
| `RabbitMqPublisherConfirms` | Shared helper confirms |
| `RabbitMqConnectionPoolRegistry` | P2-PF: register dispose on shutdown |
| `RabbitMqConnectionPoolsBootstrap` | P2-PF: `SetMetrics` для publish/RPC pools |
| `RabbitMqConnectionPoolsShutdownHostedService` | P2-PF: `DisposeAll` при host stop (net8) |
| `RabbitMqReplyPublisherPool` | P2-PF: shutdown dispose |
| `IIntegrationFlowMetrics` | `RecordConnectionPoolSize(kind, size)` |
| `ILeaveOpenOnDispose` | Shared в `00InnerUsage/RabbitMq/` |

---

## Тесты

| Тест | ID |
|------|-----|
| `PopulateProfile_CopiesSslFields` | P2-F |
| `PopulateProfile_CopiesSslAndReuseConnectionFields` | P2-F, P2-D |
| `LoadFromFile_ReadsReuseReplyConnectionAndSslSettings` | P2-F (RPC, уже был) |
| `PopulateProfile_CopiesPublisherConfirmsAndReplyMandatory` | P2-RPC-C/M |
| `RabbitMqPublishConnectionPoolTests` | P2-D, P2-PF |
| `RecordConnectionPoolSize_UpdatesGauge` | P2-PF |

Integration E2E для pool/TLS в CI **не добавлялись** (TLS в Testcontainers — out of scope плана).

---

## Документация

| Документ | Обновление |
|----------|------------|
| `README.md` | AMQPS для трёх профилей, `ReuseConnection`, RPC confirms/mandatory, gauge |
| `plans/2026-07-06_1519-rabbitmq-p2-resilience-hardening.md` | DoD core ✅ |
| `2026-07-04_2352-rabbitmq-full-analysis.md` | P2 core → Закрыт |
| `2026-07-05_1455-integrationflow-full-analysis.md` | P2 → Закрыт (core) |
| `2026-07-06_1519-remaining-backlog-summary.md` | Следующий приоритет → P3 |

---

## Конфигурация (примеры)

**AMQPS + publish pool:**

```json
{
  "RabbitMqPublish": {
    "OrdersOut": {
      "HostName": "rabbit.example.com",
      "Port": 5671,
      "SslEnabled": true,
      "SslServerName": "rabbit.example.com",
      "PublishTarget": "Queue",
      "QueueName": "orders.outbox",
      "ReuseConnection": true
    }
  }
}
```

**RPC confirms + mandatory reply (server):**

```json
{
  "RabbitMqRequestReply": {
    "OrdersRpc": {
      "PublisherConfirmsEnabled": true,
      "ConfirmTimeoutSeconds": 30,
      "ReplyMandatory": true
    }
  }
}
```

**Метрика pool:** `integrationflow.connection.pool.size` (tags: `kind=rpc|publish`).

---

## Следующий шаг

| Приоритет | Задача |
|-----------|--------|
| **Ops** | NuGet publish v1.0.0 / tag `v1.0.1` после commit P2 |
| **P3** | Health checks, env overlay, tracing (optional) |
| **Optional** | P2-CB, P2-HA, pool idle eviction — v1.1 |

Backlog: [`2026-07-06_1519-remaining-backlog-summary.md`](2026-07-06_1519-remaining-backlog-summary.md).
