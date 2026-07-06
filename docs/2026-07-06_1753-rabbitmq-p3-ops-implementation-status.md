# Статус реализации RabbitMQ P3 — ops readiness (P3-1 … P3-7)

**Статус:** выполнено  
**Создан:** 2026-07-06 17:53 (UTC+3)  
**Обновлён:** 2026-07-06 18:16 (UTC+3)  
**План:** [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md)  
**Предшественник:** [`2026-07-06_1617-rabbitmq-p2-implementation-status.md`](2026-07-06_1617-rabbitmq-p2-implementation-status.md) (P2 core ✅)  
**Runbook:** [`runbooks/2026-07-04_0845-metrics-and-alerting.md`](runbooks/2026-07-04_0845-metrics-and-alerting.md)

---

## Итог

Epic **P3 «Production observability & config»** закрыт. Все семь задач реализованы и задокументированы.

| ID | Задача | Статус |
|----|--------|--------|
| P3-1 | Health checks (`IHealthCheck`) | ✅ |
| P3-2 | Env / `IConfiguration` overlay | ✅ |
| P3-3 | Gauge `integrationflow.broker.connected` | ✅ |
| P3-4 | Distributed tracing | ✅ |
| P3-5 | Structured logging | ✅ |
| P3-6 | Consumer-level метрики | ✅ |
| P3-7 | Документация P3 (полная) | ✅ |

---

## P3-1 — Health checks

| Компонент | Изменение |
|-----------|-----------|
| `RabbitMqTransportHealthRegistry` | Реестр состояния transport endpoints |
| `RabbitMqListenerHealthCheck` / `RabbitMqOutboxRelayHealthCheck` / `RabbitMqRpcCorrelationHealthCheck` | `IHealthCheck` для трёх workers |
| `AddIntegrationFlowRabbitMqHealthChecks()` | DI extension, tag `ready` |

```csharp
services.AddIntegrationFlowRabbitMqHealthChecks(options =>
{
    options.MaxReconnectAttemptsBeforeUnhealthy = 5;
    options.OutboxRelayMaxConsecutiveFailures = 5;
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

| Check | Имя |
|-------|-----|
| Listener | `integrationflow.rabbitmq.listener` |
| Outbox relay | `integrationflow.rabbitmq.outbox_relay` |
| RPC correlation | `integrationflow.rabbitmq.rpc_correlation` |

---

## P3-2 — Env / IConfiguration overlay

Приоритет: `rabbitmq.json` → environment variables → `IConfiguration`.

```bash
export RabbitMq__Inbox__HostName=rabbit.prod.internal
export RabbitMq__Inbox__Password=secret
```

```csharp
services.AddIntegrationFlowRabbitMq(context.Configuration);
```

---

## P3-3 — Broker connectivity gauge

| Метрика | Тип | Tags |
|---------|-----|------|
| `integrationflow.broker.connected` | Gauge | `profile`, `kind` (`listener`, `outbox_relay`, `rpc_correlation`) |

---

## P3-4 — Distributed tracing

| Компонент | Изменение |
|-----------|-----------|
| `IntegrationFlowRabbitMqActivitySource` | Публичное имя ActivitySource |
| `RabbitMqTracePropagation` | W3C `traceparent` / `tracestate` в AMQP headers |
| `RabbitMqDistributedTracing` | Spans publish/consume/RPC |

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(IntegrationFlowRabbitMqActivitySource.Name));
```

---

## P3-5 — Structured logging

Поля scope: `integrationflow.profile`, `integrationflow.message_id`, `integrationflow.correlation_id`, `integrationflow.delivery_tag`, `integrationflow.kind`, `integrationflow.outcome`.

Работает через `ILogger.BeginScope` при `AddIntegrationFlow()` + host `ILoggerFactory`.

---

## P3-6 — Consumer outcome metrics

| Метрика | Tags | `reason` values |
|---------|------|-----------------|
| `integrationflow.message.consumer.outcome` | `profile`, `reason` | `nack`, `requeue`, `dedup_skip`, `in_progress_requeue` |

API: `IIntegrationFlowMetrics.RecordConsumerOutcome(profile, reason)`.

---

## P3-7 — Документация

| Документ | Содержание |
|----------|------------|
| `README.md` | Production quick-start, P3 checklist, metrics/logging/tracing |
| [`runbooks/2026-07-04_0845-metrics-and-alerting.md`](runbooks/2026-07-04_0845-metrics-and-alerting.md) | Setup, health alerts, пороги P1–P3, PromQL, logging/tracing troubleshooting |
| Этот документ | Статус реализации P3-1 … P3-7 |

---

## Тесты (P3)

| Тест | ID |
|------|-----|
| `RabbitMqTransportHealthRegistryTests` | P3-1, P3-3 |
| `RabbitMqHealthCheckTests` | P3-1 |
| `RabbitMqHealthChecksRegistrationTests` | P3-1 |
| `RabbitMqConfigurationOverlayTests` | P3-2 |
| `RecordBrokerConnected_UpdatesGauge` | P3-3 |
| `IntegrationStructuredLoggingTests` | P3-5 |
| `RabbitMqReceivedMessageHandlerStructuredLoggingTests` | P3-5 |
| `RecordConsumerOutcome_*` | P3-6 |
| `RabbitMqReceivedMessageHandlerTests` (metrics) | P3-6 |
| `RabbitMqTracePropagationTests` | P3-4 |

---

## Следующий шаг

| Приоритет | Задача |
|-----------|--------|
| **P4** | Shared connection profiles, Headers API |
| **Ops** | NuGet publish v1.0.0 / v1.0.1 |
| **Tests** | T-1 chaos E2E, T-3 DLQ E2E |

Backlog: [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md).
