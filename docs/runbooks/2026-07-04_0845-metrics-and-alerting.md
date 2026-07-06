# Runbook: metrics and alerting

**Статус:** актуально  
**Создан:** 2026-07-04 08:45 (UTC+3)  
**Обновлён:** 2026-07-06 18:16 (UTC+3)  
**Охват:** P3 ops readiness (P3-1 … P3-7)  
**Статус P3:** [`../2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md`](../2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md)

Рекомендуемые алерты для приложений с `AddIntegrationFlowOpenTelemetryMetrics()`, health checks и OpenTelemetry tracing/logging.

---

## Production setup (P3)

Минимальная регистрация в ASP.NET Core / Worker (.NET 8+):

```csharp
services.AddIntegrationFlow();
services.AddIntegrationFlowRabbitMq(configuration);
services.AddIntegrationFlowRabbitMqListener("Inbox", handler);
services.AddIntegrationFlowOutboxRelay();
services.AddIntegrationFlowOpenTelemetryMetrics();
services.AddIntegrationFlowRabbitMqHealthChecks(options =>
{
    options.MaxReconnectAttemptsBeforeUnhealthy = 5;
    options.OutboxRelayMaxConsecutiveFailures = 5;
});

services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("IntegrationFlow").AddPrometheusExporter())
    .WithTracing(t => t.AddSource(IntegrationFlowRabbitMqActivitySource.Name).AddOtlpExporter());
```

Kubernetes:

| Probe | Endpoint | Predicate |
|-------|----------|-----------|
| readiness | `/health/ready` | tag `ready` |
| metrics | `/metrics` | Prometheus scrape |

---

## Health checks (P3-1)

| Check | Имя | Unhealthy когда |
|-------|-----|-----------------|
| Listener | `integrationflow.rabbitmq.listener` | Нет соединения или `ReconnectAttempts ≥ MaxReconnectAttemptsBeforeUnhealthy` |
| Outbox relay | `integrationflow.rabbitmq.outbox_relay` | Подряд ≥ `OutboxRelayMaxConsecutiveFailures` неудачных batch |
| RPC correlation | `integrationflow.rabbitmq.rpc_correlation` | Response consumer отключён / reconnect ≥ порога |

**Алерт readiness (любой check unhealthy > 2 мин):**

```promql
avg_over_time(probe_success{job="integrationflow", probe="readiness"}[2m]) < 1
```

**Действие:** проверить `integrationflow.broker.connected`, логи reconnect, credentials/TLS, broker uptime.

---

## Метрики

| Метрика | Тип | Когда смотреть |
|---------|-----|----------------|
| `integrationflow.outbox.pending` | Gauge | Backlog outbox |
| `integrationflow.outbox.relay.abandoned` | Counter | Сообщения исчерпали max attempts |
| `integrationflow.outbox.relay.failed` | Counter | Ошибки publish relay |
| `integrationflow.message.processed` | Counter | Consumer throughput / failures |
| `integrationflow.message.processing.duration` | Histogram | Latency обработки |
| `integrationflow.message.consumer.outcome` | Counter | Consumer ack outcomes (`profile`, `reason`) |
| `integrationflow.requestreply.completed` | Counter | RPC round-trip (`profile`, `success`, `timeout`) |
| `integrationflow.requestreply.duration` | Histogram | Latency RPC (секунды) |
| `integrationflow.listener.reconnect` | Counter | Переподключения listener (`profile`) |
| `integrationflow.message.shutdown_requeue` | Counter | Nack requeue при shutdown listener (`profile`) |
| `integrationflow.connection.pool.size` | Gauge | Размер TCP pool (`kind=rpc|publish`) |
| `integrationflow.broker.connected` | Gauge | Broker connectivity (`profile`, `kind`; 1=connected) |

### Consumer outcome `reason` (P3-6)

| `reason` | Значение |
|----------|----------|
| `nack` | Nack без requeue → DLQ / poison |
| `requeue` | Nack с requeue после ошибки handler |
| `dedup_skip` | Сообщение уже обработано (dedup store) |
| `in_progress_requeue` | Параллельная доставка того же `MessageId` |

---

## Пороги алертов (рекомендуемые)

| Severity | Алерт | PromQL (пример) | Порог | Действие |
|----------|-------|-----------------|-------|----------|
| **P1** | Listener offline | `integrationflow_broker_connected{kind="listener"} == 0` | немедленно | Broker outage, credentials, TLS |
| **P1** | Readiness unhealthy | `probe_success{probe="readiness"} == 0` | > 2 мин | См. health checks |
| **P2** | Outbox backlog | `integrationflow_outbox_pending` | > 100 | Relay worker, DB locks, broker |
| **P2** | Abandoned outbox | `increase(integrationflow_outbox_relay_abandoned_total[5m])` | > 0 | Replay runbook |
| **P2** | Consumer failures | `increase(integrationflow_message_processed_total{success="false"}[5m])` | > 0 | Handler logs, DLQ |
| **P2** | Consumer nack (DLQ) | `increase(integrationflow_message_consumer_outcome_total{reason="nack"}[5m])` | > 0 | Poison messages, `MaxRetryCount` |
| **P2** | RPC failures | `increase(integrationflow_requestreply_completed_total{success="false"}[5m])` | > 0 | Server handler, broker |
| **P2** | RPC timeouts | `increase(integrationflow_requestreply_completed_total{timeout="true"}[5m])` | > 0 | `ResponseTimeoutSeconds`, server latency |
| **P3** | Relay failures | `increase(integrationflow_outbox_relay_failed_total[5m])` | > 5 | Topology, credentials |
| **P3** | Listener reconnects | `increase(integrationflow_listener_reconnect_total[5m])` | > 0 | Нестабильная сеть / broker |
| **P3** | Consumer requeue spike | `increase(integrationflow_message_consumer_outcome_total{reason="requeue"}[5m])` | > 20 | Transient errors, handler latency |
| **P3** | Dedup skip spike | `increase(integrationflow_message_consumer_outcome_total{reason="dedup_skip"}[5m])` | > 50 | At-least-once redelivery |
| **P3** | In-progress requeue | `increase(integrationflow_message_consumer_outcome_total{reason="in_progress_requeue"}[5m])` | > 10 | Prefetch, `ProcessingLockDuration` |
| **P3** | Shutdown requeues | `increase(integrationflow_message_shutdown_requeue_total[5m])` | > 10 | Rolling deploy, drain timeout |
| **P3** | RPC p99 latency | `histogram_quantile(0.99, rate(integrationflow_requestreply_duration_bucket[5m]))` | > 5s | Pool reuse, server load |

> Имена серий зависят от Prometheus exporter (underscore vs dot). Проверьте `/metrics` endpoint приложения.  
> Пороги — стартовые; калибруйте под SLA и нагрузку.

---

## PromQL (копировать)

**Outbox backlog:**

```promql
integrationflow_outbox_pending > 100
```

**Abandoned messages (за 5 мин):**

```promql
increase(integrationflow_outbox_relay_abandoned_total[5m]) > 0
```

**Consumer failures (за 5 мин):**

```promql
increase(integrationflow_message_processed_total{success="false"}[5m]) > 0
```

**Relay failures (за 5 мин):**

```promql
increase(integrationflow_outbox_relay_failed_total[5m]) > 5
```

**RPC failures (за 5 мин):**

```promql
increase(integrationflow_requestreply_completed_total{success="false"}[5m]) > 0
```

**RPC timeouts (за 5 мин):**

```promql
increase(integrationflow_requestreply_completed_total{timeout="true"}[5m]) > 0
```

**RPC p99 latency:**

```promql
histogram_quantile(0.99, rate(integrationflow_requestreply_duration_bucket[5m])) > 5
```

**Listener reconnects (за 5 мин):**

```promql
increase(integrationflow_listener_reconnect_total[5m]) > 0
```

**Shutdown requeues (за 5 мин):**

```promql
increase(integrationflow_message_shutdown_requeue_total[5m]) > 10
```

**Consumer nack без requeue (за 5 мин):**

```promql
increase(integrationflow_message_consumer_outcome_total{reason="nack"}[5m]) > 0
```

**Consumer requeue spike (за 5 мин):**

```promql
increase(integrationflow_message_consumer_outcome_total{reason="requeue"}[5m]) > 20
```

**Dedup skip spike (за 5 мин):**

```promql
increase(integrationflow_message_consumer_outcome_total{reason="dedup_skip"}[5m]) > 50
```

**In-progress requeue (за 5 мин):**

```promql
increase(integrationflow_message_consumer_outcome_total{reason="in_progress_requeue"}[5m]) > 10
```

**Listener offline (текущее состояние):**

```promql
integrationflow_broker_connected{kind="listener"} == 0
```

**Listener offline дольше 2 мин:**

```promql
min_over_time(integrationflow_broker_connected{kind="listener"}[2m]) == 0
```

**Outbox relay / RPC correlation offline:**

```promql
integrationflow_broker_connected{kind="outbox_relay"} == 0
integrationflow_broker_connected{kind="rpc_correlation"} == 0
```

---

## Structured logging (P3-5)

Поля scope: `integrationflow.profile`, `integrationflow.message_id`, `integrationflow.correlation_id`, `integrationflow.delivery_tag`, `integrationflow.kind`, `integrationflow.outcome`.

**Loki / LogQL — найти nack по профилю:**

```logql
{app="orders-service"} | json | integrationflow_outcome="nack" | integrationflow_profile="Inbox"
```

**Корреляция с trace (OTel logs):**

Фильтр по `integrationflow.message_id` + `trace_id` в том же log stream.

---

## Distributed tracing (P3-4)

ActivitySource: `IntegrationFlow.RabbitMq`. Spans: `rabbitmq.publish`, `rabbitmq.receive`, `rabbitmq.request`, `rabbitmq.reply`, `rabbitmq.response`.

**Проверка propagation:** в Jaeger/Tempo ищите trace с цепочкой `rabbitmq.publish` → `rabbitmq.receive` → `rabbitmq.reply` → `rabbitmq.response`.

**Если trace обрывается:**

1. Host app регистрирует `AddSource(IntegrationFlowRabbitMqActivitySource.Name)`?
2. Sampling не отбрасывает consumer spans?
3. На netstandard2.0 extract parent — best-effort (см. README).

---

## Config overlay (P3-2)

Приоритет: `rabbitmq.json` → env vars → `IConfiguration` (appsettings).

```bash
RabbitMq__Inbox__HostName=rabbit.prod.internal
RabbitMq__Inbox__Password=${RABBITMQ_PASSWORD}
RabbitMqPublish__OrdersOut__HostName=rabbit.prod.internal
```

**Prod checklist:**

- [ ] Пароли только в secrets / env, не в git
- [ ] `AddIntegrationFlowRabbitMq(builder.Configuration)` в host
- [ ] Отдельные профили для dev/stage/prod

---

## Действия (кратко)

| Алерт | Действие |
|-------|----------|
| pending высокий | Проверить relay worker, broker, DB locks |
| abandoned > 0 | См. [`2026-07-03_2216-abandoned-outbox-replay.md`](2026-07-03_2216-abandoned-outbox-replay.md) |
| consumer failures | Логи handler (`integrationflow.outcome`), DLQ, poison messages |
| consumer nack | Handler errors, DLQ topology, `MaxRetryCount` |
| consumer requeue spike | Transient errors, broker pressure, handler latency |
| dedup skip spike | At-least-once redelivery; publisher retries |
| in_progress requeue | Параллельная доставка; `ProcessingLockDuration`, prefetch |
| relay failed | Broker connectivity, topology, credentials |
| RPC failures | Логи transmitter, broker, server handler |
| RPC timeouts | `ResponseTimeoutSeconds`; server latency |
| RPC p99 высокий | `ReuseConnection`, `ReuseReplyConnection`, нагрузка |
| broker connected = 0 | Reconnect loop, broker outage, credentials/TLS |
| readiness unhealthy | Health check details + `broker.connected` gauge |
