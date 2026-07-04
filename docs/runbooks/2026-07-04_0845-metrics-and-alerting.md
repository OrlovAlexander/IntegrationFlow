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

> Имена серий зависят от Prometheus exporter (underscore vs dot). Проверьте `/metrics` endpoint приложения.

---

## Действия

| Алерт | Действие |
|-------|----------|
| pending высокий | Проверить relay worker, broker, DB locks |
| abandoned > 0 | См. [`2026-07-03_2216-abandoned-outbox-replay.md`](2026-07-03_2216-abandoned-outbox-replay.md) |
| consumer failures | Логи handler, DLQ, poison messages |
| relay failed | Broker connectivity, topology, credentials |
