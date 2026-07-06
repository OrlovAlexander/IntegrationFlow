# Runbook: metrics and alerting

**Статус:** актуально  
**Создан:** 2026-07-04 08:45 (UTC+3)

Рекомендуемые алерты для приложений с `AddIntegrationFlowOpenTelemetryMetrics()` и Prometheus/Grafana.

---

## Метрики

| Метрика | Тип | Когда смотреть |
|---------|-----|----------------|
| `integrationflow.outbox.pending` | Gauge | Backlog outbox |
| `integrationflow.outbox.relay.abandoned` | Counter | Сообщения исчерпали max attempts |
| `integrationflow.outbox.relay.failed` | Counter | Ошибки publish relay |
| `integrationflow.message.processed` | Counter | Consumer throughput / failures |
| `integrationflow.message.processing.duration` | Histogram | Latency обработки |
| `integrationflow.requestreply.completed` | Counter | RPC round-trip (`profile`, `success`, `timeout`) |
| `integrationflow.requestreply.duration` | Histogram | Latency RPC (секунды) |
| `integrationflow.listener.reconnect` | Counter | Переподключения listener (`profile`) |
| `integrationflow.message.shutdown_requeue` | Counter | Nack requeue при shutdown listener (`profile`) |
| `integrationflow.connection.pool.size` | Gauge | Размер TCP pool (`kind=rpc|publish`) |

---

## Рекомендуемые алерты (Prometheus)

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

> Имена серий зависят от Prometheus exporter (underscore vs dot). Проверьте `/metrics` endpoint приложения.

---

## Действия

| Алерт | Действие |
|-------|----------|
| pending высокий | Проверить relay worker, broker, DB locks |
| abandoned > 0 | См. [`2026-07-03_2216-abandoned-outbox-replay.md`](2026-07-03_2216-abandoned-outbox-replay.md) |
| consumer failures | Логи handler, DLQ, poison messages |
| relay failed | Broker connectivity, topology, credentials |
| RPC failures | Логи transmitter, broker, server handler |
| RPC timeouts | Увеличить `ResponseTimeoutSeconds`; проверить server latency |
| RPC p99 высокий | `ReuseConnection`, server `ReuseReplyConnection`, нагрузка |
