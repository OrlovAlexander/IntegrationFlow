# Статус реализации RabbitMQ P3 — ops readiness (P3-1 … P3-3)

**Статус:** выполнено (частично — P3-1 … P3-3)  
**Создан:** 2026-07-06 17:53 (UTC+3)  
**Обновлён:** 2026-07-06 17:53 (UTC+3)  
**План:** [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md)  
**Предшественник:** [`2026-07-06_1617-rabbitmq-p2-implementation-status.md`](2026-07-06_1617-rabbitmq-p2-implementation-status.md) (P2 core ✅)  
**Связанные документы:** [`2026-07-04_2352-rabbitmq-full-analysis.md`](2026-07-04_2352-rabbitmq-full-analysis.md), [`runbooks/2026-07-04_0845-metrics-and-alerting.md`](runbooks/2026-07-04_0845-metrics-and-alerting.md)

---

## Итог

Закрыты первые три задачи P3 ops readiness: **health checks**, **config overlay**, **broker connectivity gauge**. Остаются P3-4 … P3-7.

| ID | Задача | Статус |
|----|--------|--------|
| P3-1 | Health checks (`IHealthCheck`) | ✅ |
| P3-2 | Env / `IConfiguration` overlay | ✅ |
| P3-3 | Gauge `integrationflow.broker.connected` | ✅ |
| P3-4 | Distributed tracing | ⏸ Открыт |
| P3-5 | Structured logging | ⏸ Открыт |
| P3-6 | Consumer-level метрики | ⏸ Открыт |
| P3-7 | Документация P3 (полная) | ⏸ Частично (README + runbook для P3-1/3) |

---

## P3-1 — Health checks

### Изменения в коде

| Компонент | Изменение |
|-----------|-----------|
| `RabbitMqTransportHealthRegistry` | Реестр состояния transport endpoints |
| `RabbitMqListenerHealthCheck` / `RabbitMqOutboxRelayHealthCheck` / `RabbitMqRpcCorrelationHealthCheck` | `IHealthCheck` для трёх workers |
| `AddIntegrationFlowRabbitMqHealthChecks()` | DI extension, tag `ready` |
| `RabbitMqListenerWorker` | Reporting connect / reconnect / disconnect |
| `RabbitMqRpcResponseCorrelationHostedService` | Reconnect loop + health reporting |
| `OutboxRelayService` | Batch success/failure reporting |

### Конфигурация

```csharp
services.AddIntegrationFlow();
services.AddIntegrationFlowRabbitMqListener("Inbox", _ => { });
services.AddIntegrationFlowRabbitMqHealthChecks(options =>
{
    options.MaxReconnectAttemptsBeforeUnhealthy = 5;
    options.OutboxRelayMaxConsecutiveFailures = 5;
});
```

### Health check names

| Check | Имя |
|-------|-----|
| Listener | `integrationflow.rabbitmq.listener` |
| Outbox relay | `integrationflow.rabbitmq.outbox_relay` |
| RPC correlation | `integrationflow.rabbitmq.rpc_correlation` |

---

## P3-2 — Env / IConfiguration overlay

### Изменения в коде

| Компонент | Изменение |
|-----------|-----------|
| `RabbitMqConfigurationComposition` | JSON → env vars → overlay |
| `RabbitMqConfigurationLoader` / `RabbitMqPublishConfigurationLoader` / `RabbitMqRequestReplyConfigurationLoader` | `LoadProfile(name, IConfiguration)`, `LoadAll(IConfiguration)` |
| `AddIntegrationFlowRabbitMq(IConfiguration)` | Host configuration overlay |

### Приоритет источников

```
rabbitmq.json  →  environment variables  →  IConfiguration overlay
```

### Примеры

**Environment variables:**

```bash
export RabbitMq__Inbox__HostName=rabbit.prod.internal
export RabbitMq__Inbox__Password=secret
```

**ASP.NET Core / Worker:**

```csharp
services.AddIntegrationFlow();
services.AddIntegrationFlowRabbitMq(context.Configuration);
```

---

## P3-3 — Broker connectivity gauge

### Изменения в коде

| Компонент | Изменение |
|-----------|-----------|
| `IIntegrationFlowMetrics.RecordBrokerConnected` | API метрики |
| `IntegrationFlowMeter` | Observable gauge `integrationflow.broker.connected` |
| `RabbitMqTransportHealthRegistry` | Publish gauge при смене состояния |
| `RabbitMqConnectionPoolsBootstrap` | `SetMetrics` для registry |
| `AddIntegrationFlowOpenTelemetryMetrics` | Wire registry + pools |

### Метрика

| Имя | Тип | Tags | Значение |
|-----|-----|------|----------|
| `integrationflow.broker.connected` | Gauge | `profile`, `kind` | `1` = connected, `0` = disconnected |

**kind:** `listener`, `outbox_relay`, `rpc_correlation`

### Алерт (Prometheus)

```promql
integrationflow_broker_connected{kind="listener"} == 0
```

Runbook: [`runbooks/2026-07-04_0845-metrics-and-alerting.md`](runbooks/2026-07-04_0845-metrics-and-alerting.md)

---

## Тесты

| Тест | ID |
|------|-----|
| `RabbitMqTransportHealthRegistryTests` | P3-1, P3-3 |
| `RabbitMqHealthCheckTests` | P3-1 |
| `RabbitMqHealthChecksRegistrationTests` | P3-1 |
| `RabbitMqConfigurationOverlayTests` | P3-2 |
| `RecordBrokerConnected_UpdatesGauge` | P3-3 |

Unit-тесты: **159** passed (на момент реализации).

---

## Документация

| Документ | Обновление |
|----------|------------|
| `README.md` | Health checks, config overlay, gauge |
| `runbooks/2026-07-04_0845-metrics-and-alerting.md` | `broker.connected` + алерты |

---

## Следующий шаг

| Приоритет | Задача |
|-----------|--------|
| **P3-4** | Distributed tracing (`traceparent` через AMQP headers) |
| **P3-5** | Structured logging |
| **P3-6** | Consumer-level counters (`nack`, `requeue`, …) |
| **Ops** | NuGet publish v1.0.0 / v1.0.1 |

Backlog: [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md).
