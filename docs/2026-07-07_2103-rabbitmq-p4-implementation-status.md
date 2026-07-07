# Статус реализации RabbitMQ P4 — DX (P4-4, P4-5)

**Статус:** частично выполнено (P4-4, P4-5 ✅; P4-6, P4-7 — backlog)  
**Создан:** 2026-07-07 21:03 (UTC+3)  
**План:** [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md)  
**Предшественник:** [`2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md`](2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md) (P3 ✅)  
**Коммиты:** `732a08f` (P4-4), `4d0d977` (P4-5)

---

## Итог

| ID | Задача | Статус |
|----|--------|--------|
| P4-4 | Hosted listener netstandard2.0 | ✅ |
| P4-5 | AMQP priority / expiration | ✅ |
| P4-6 | Topology helpers (dev) | backlog |
| P4-7 | Consumer tag / exclusive | backlog |

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

## Следующий шаг

| Приоритет | Задача |
|-----------|--------|
| **Код** | P4-6 — `DeclareTopologyOnStartup` |
| **Код** | P4-7 — `ConsumerTag`, `Exclusive` |
| **Ops** | NuGet publish v1.0.0 / v1.0.1 |

Краткий backlog: [`2026-07-06_1519-remaining-backlog-summary.md`](2026-07-06_1519-remaining-backlog-summary.md).
