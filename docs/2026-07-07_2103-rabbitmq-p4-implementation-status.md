# Статус реализации RabbitMQ P4 — DX (epic P4-1 … P4-7)

**Статус:** выполнено (epic P4 ✅)  
**Создан:** 2026-07-07 21:03 (UTC+3)  
**Обновлён:** 2026-07-07 22:01 (UTC+3)  
**План:** [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md)  
**Предшественник:** [`2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md`](2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md) (P3 ✅)  
**Коммиты:** P4-4 `732a08f`, P4-5 `4d0d977`, P4-6 `647d810`/`3a2cce7`, P4-7 `4bb09c6`

---

## Итог

| ID | Задача | Статус |
|----|--------|--------|
| P4-1 | Shared connection profiles | ✅ |
| P4-2 | Headers API | ✅ |
| P4-3 | Sample hosted RPC server | ✅ |
| P4-4 | Hosted listener netstandard2.0 | ✅ |
| P4-5 | AMQP priority / expiration | ✅ |
| P4-6 | Topology helpers (dev) | ✅ |
| P4-7 | Consumer tag / exclusive | ✅ |

---

## P4-4 — Hosted listener netstandard2.0

`AddIntegrationFlowRabbitMqListener` регистрирует `IHostedService` на **netstandard2.0** и **net8.0**.

| Компонент | Изменение |
|-----------|-----------|
| `IntegrationFlow.Core.csproj` | `Microsoft.Extensions.Hosting.Abstractions` для всех TFM |
| `ReceiveAndProcessHostedService` | Без `#if NET8_0_OR_GREATER` |
| `ServiceCollectionReceiveAndProcessExtensions` | Регистрация listener на всех TFM |
| `IntegrationFlow.Core.NetStandard2.Tests` | net472 runner → netstandard2.0-сборка Core |

```csharp
// netstandard2.0 и net8.0
services.AddIntegrationFlow();
services.AddIntegrationFlowRabbitMqListener("Inbox", message => { /* ... */ });
```

**Ограничение:** health checks, outbox/RPC hosted workers, pool shutdown — по-прежнему **net8.0 only** (`#if NET8_0_OR_GREATER`).

---

## P4-5 — AMQP priority / expiration

Опциональные поля в `RabbitMqPublish` и `RabbitMqRequestReply`; mapping через `RabbitMqBasicPropertiesMapper`.

| Поле JSON | Тип | AMQP |
|-----------|-----|------|
| `Priority` | `byte?` (0–255) | `basic.properties.priority` |
| `ExpirationMilliseconds` | `int?` (> 0) | `basic.properties.expiration` (строка мс) |

**Применяется в:**
- `RabbitMqPublishTransmitter` (SentAndForgot)
- `RabbitMqRequestReplyTransmitter` (sync RPC)
- `RpcPendingRelayService` (AsyncOutbox RPC)

```json
{
  "RabbitMqPublish": {
    "OrdersOut": {
      "HostName": "localhost",
      "QueueName": "orders.out",
      "PublishTarget": "Queue",
      "Priority": 5,
      "ExpirationMilliseconds": 120000
    }
  }
}
```

**Не задано (`null`)** — свойство не выставляется явно (обратная совместимость).

---

## P4-6 — Topology helpers (dev)

Opt-in active declare для local/dev; по умолчанию — passive declare (production-safe).

| Поле JSON | Тип | По умолчанию | Поведение |
|-----------|-----|--------------|-----------|
| `ValidateTopology` | `bool` | `true` | `false` — пропустить declare |
| `DeclareTopologyOnStartup` | `bool` | `false` | `true` — active `QueueDeclare` / `ExchangeDeclare` |
| `ExchangeType` | `string` | `direct` | Тип exchange при active declare (publish/RPC) |

**Компоненты:**
- `RabbitMqTopologyHelper` — passive/active ensure для queue и exchange; `LogWarn` при первом active declare
- Подключено в listener, publish, sync RPC, AsyncOutbox relay, RPC correlation hosted service
- Unit-тесты: `RabbitMqTopologyHelperTests`, loader-тесты для трёх конфигов
- Runbook: [`runbooks/2026-07-07_2137-rabbitmq-topology-adoption.md`](runbooks/2026-07-07_2137-rabbitmq-topology-adoption.md)

```json
{
  "RabbitMq": {
    "Inbox": {
      "HostName": "localhost",
      "QueueName": "inbox.dev",
      "ValidateTopology": true,
      "DeclareTopologyOnStartup": true
    }
  }
}
```

> **Production:** оставляйте `DeclareTopologyOnStartup = false`; создавайте очереди/DLQ/bindings через IaC. Active declare не задаёт DLQ, TTL и bindings. Runbook: [`runbooks/2026-07-07_2137-rabbitmq-topology-adoption.md`](runbooks/2026-07-07_2137-rabbitmq-topology-adoption.md).

---

## P4-7 — Consumer tag / exclusive

Поля в `RabbitMqConfiguration` для `basic.consume`:

| Поле JSON | Тип | По умолчанию | Поведение |
|-----------|-----|--------------|-----------|
| `ConsumerTag` | `string` | `""` | Пусто — брокер генерирует tag; иначе фиксированный tag |
| `Exclusive` | `bool` | `false` | `true` — exclusive consumer (один consumer на очередь) |

Подключено в `RabbitMqListenerWorker.BasicConsume`. Документация SAC — README (consumer params).

```json
{
  "RabbitMq": {
    "OrdersInbox": {
      "QueueName": "orders.inbox",
      "ConsumerTag": "orders-inbox-pod-1",
      "Exclusive": true
    }
  }
}
```

---

## Следующий шаг

| Приоритет | Задача |
|-----------|--------|
| **Ops** | NuGet publish v1.0.0 / v1.0.1 |
| **Код (optional)** | T-7 — health check unit tests; v1.1 P2-CB/P2-HA |

Краткий backlog: [`2026-07-06_1519-remaining-backlog-summary.md`](2026-07-06_1519-remaining-backlog-summary.md).
