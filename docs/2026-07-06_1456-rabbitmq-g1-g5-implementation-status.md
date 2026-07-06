# Статус реализации RabbitMQ transport gaps G1–G5

**Статус:** выполнено  
**Создан:** 2026-07-06 14:56 (UTC+3)  
**Обновлён:** 2026-07-06 14:56 (UTC+3)  
**План:** [`plans/2026-07-06_1445-rabbitmq-g1-g5-mitigation.md`](plans/2026-07-06_1445-rabbitmq-g1-g5-mitigation.md)  
**Связанные документы:** [`2026-07-04_2352-rabbitmq-full-analysis.md`](2026-07-04_2352-rabbitmq-full-analysis.md), [`2026-07-05_1455-integrationflow-full-analysis.md`](2026-07-05_1455-integrationflow-full-analysis.md)

---

## Итог

Все пять P1 transport gaps **закрыты** в коде, unit- и integration-тестах, README и runbook метрик.

| ID | Задача | Статус |
|----|--------|--------|
| G3 | `Copy()` для `RequeueOnFailure` / `MaxRetryCount` | ✅ |
| G5 | Channel sync в `RabbitMqRpcResponseCorrelationHostedService` | ✅ |
| G2 | Graceful shutdown: in-flight drain + nack requeue | ✅ |
| G1 | Reconnect loop с exponential backoff | ✅ |
| G4 | `ManualReplyAck` для sync RPC reply consumer | ✅ |

---

## Изменения в коде

| Компонент | Изменение |
|-----------|-----------|
| `RabbitMqConfigurationLoader.Copy()` | G3: retry-поля |
| `RabbitMqRpcResponseCorrelationHostedService` | G5: `RabbitMqChannelAcknowledgement` |
| `RabbitMqListenerWorker` | G1: reconnect loop; G2: `BasicCancel` + drain |
| `RabbitMqListenerInFlightTracker` | G2: новый helper |
| `RabbitMqReceivedMessageHandler` | G2: nack requeue при shutdown |
| `RabbitMqRequestReplyConnection` | G4: `ManualReplyAck` |
| `IIntegrationFlowMetrics` | `RecordListenerReconnect`, `RecordListenerShutdownRequeue` |

---

## Тесты

| Тест | Gap |
|------|-----|
| `PopulateProfile_CopiesRetryPolicyFields` | G3 |
| `HandleAsync_NacksRequeueWhenCancellation*` | G2 |
| `RabbitMqListenerInFlightTrackerTests` | G2 |
| `HostedService_StopAsync_DuringSlowProcessing_*` | G2 |
| `HostedService_ReconnectsAfterBrokerRestart_*` | G1 |
| `RecordListenerTransportMetrics_*` | Metrics |

---

## Документация

| Документ | Обновление |
|----------|------------|
| `README.md` | Reconnect, graceful shutdown, `ManualReplyAck`, метрики |
| `runbooks/2026-07-04_0845-metrics-and-alerting.md` | Алерты listener reconnect / shutdown requeue |
| `2026-07-04_2352-rabbitmq-full-analysis.md` | G1–G5 → Закрыт |
| `2026-07-05_1455-integrationflow-full-analysis.md` | P1 transport → Закрыт |

---

## Конфигурация

**Graceful shutdown:** in-flight drain timeout — 30 с (константа в worker).

**Reconnect:** backoff 1 → 2 → 4 → … → max 30 с (константа в worker).

**Manual reply ack (sync RPC):**

```json
{
  "RabbitMqRequestReply": {
    "OrdersRpc": {
      "ManualReplyAck": true
    }
  }
}
```

Default `false` — backward compatible.

---

## Метрики

| Instrument | Имя |
|------------|-----|
| Counter | `integrationflow.listener.reconnect` (`profile`) |
| Counter | `integrationflow.message.shutdown_requeue` (`profile`) |
