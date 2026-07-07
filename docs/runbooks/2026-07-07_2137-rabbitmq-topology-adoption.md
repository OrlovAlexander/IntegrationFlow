# Runbook: RabbitMQ topology adoption (passive vs active declare)

**Статус:** актуально  
**Создан:** 2026-07-07 21:37 (UTC+3)  
**Задача backlog:** P4-6 (production docs)  
**Связанные документы:** [`2026-07-07_2103-rabbitmq-p4-implementation-status.md`](../2026-07-07_2103-rabbitmq-p4-implementation-status.md), [`2026-07-07_2137-rabbitmq-dlq-topology.md`](2026-07-07_2137-rabbitmq-dlq-topology.md)

IntegrationFlow проверяет или создаёт topology **только для целевой queue/exchange профиля**. Bindings, DLQ, TTL и политики — ответственность ops/IaC.

---

## Режимы

| `DeclareTopologyOnStartup` | `ValidateTopology` | Поведение |
|----------------------------|-------------------|-----------|
| `false` (default) | `true` (default) | **Passive declare** — `QueueDeclarePassive` / `ExchangeDeclarePassive`; fail fast если объект не существует |
| `true` | `true` | **Active declare** — `QueueDeclare` / `ExchangeDeclare` (durable по `Persistent` / default `true` для listener) |
| любое | `false` | Declare пропускается (тесты, pre-provisioned broker) |

Реализация: [`RabbitMqTopologyHelper`](../../src/IntegrationFlow.Core/Contexts/Integrations/00InnerUsage/RabbitMq/RabbitMqTopologyHelper.cs).

При первом active declare в лог пишется **warning** (один раз на profile+target).

---

## Local / dev

```json
{
  "RabbitMq": {
    "Inbox": {
      "HostName": "localhost",
      "QueueName": "inbox.dev",
      "ValidateTopology": true,
      "DeclareTopologyOnStartup": true
    }
  },
  "RabbitMqPublish": {
    "EventsOut": {
      "HostName": "localhost",
      "PublishTarget": "Exchange",
      "Exchange": "integration.events",
      "ExchangeType": "topic",
      "DeclareTopologyOnStartup": true
    }
  }
}
```

Подходит для быстрого старта без Terraform. **Не используйте для DLQ** — active declare создаёт только базовую queue/exchange без arguments.

---

## Production

```json
{
  "RabbitMq": {
    "OrdersInbox": {
      "HostName": "rabbitmq.prod",
      "QueueName": "orders.inbox",
      "ValidateTopology": true,
      "DeclareTopologyOnStartup": false
    }
  }
}
```

| Правило | Причина |
|---------|---------|
| `DeclareTopologyOnStartup = false` | Единый source of truth — IaC/definitions |
| Topology с DLQ/TTL до деплоя app | См. [`2026-07-07_2137-rabbitmq-dlq-topology.md`](2026-07-07_2137-rabbitmq-dlq-topology.md) |
| Passive declare в startup | Ранний fail если ops забыли queue |

Инструменты: Terraform [`rabbitmq`](https://registry.terraform.io/providers/cyrilgdn/rabbitmq/latest/docs), RabbitMQ definitions export/import, Helm charts.

---

## Где вызывается ensure

| Компонент | Queue | Exchange |
|-----------|-------|----------|
| `RabbitMqListenerWorker` | ✅ | — |
| `RabbitMqPublishTransmitter` | ✅ (target=Queue) | ✅ (target=Exchange) |
| `RabbitMqRequestReplyTransmitter` | ✅ | ✅ |
| `RpcPendingRelayService` | ✅ | ✅ |
| `RabbitMqRpcResponseCorrelationHostedService` | ✅ (reply queue) | — |

---

## Troubleshooting

| Ошибка при старте | Действие |
|-------------------|----------|
| `NOT_FOUND` passive declare | Создать queue/exchange в брокере; проверить `QueueName` / `Exchange` |
| Active declare в prod случайно включён | Выключить `DeclareTopologyOnStartup`; проверить drift DLQ args |
| `ValidateTopology = false` в prod | Включить passive declare для ранней диагностики |

---

## Связанные документы

- [`2026-07-07_2103-rabbitmq-p4-implementation-status.md`](../2026-07-07_2103-rabbitmq-p4-implementation-status.md)
- [`2026-07-04_2130-production-adoption.md`](2026-07-04_2130-production-adoption.md)
