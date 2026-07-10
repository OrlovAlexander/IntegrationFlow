# Статус реализации REST-транспорта (фазы 1–5)

**Статус:** актуально  
**Создан:** 2026-07-10 15:07 (UTC+3)  
**Обновлён:** 2026-07-10 22:10 (UTC+3)  
**План:** [`plans/2026-07-10_0853-rest-implementation.md`](plans/2026-07-10_0853-rest-implementation.md)  
**Runbook outbound:** [`runbooks/2026-07-10_1330-rest-sentandwait-adoption.md`](runbooks/2026-07-10_1330-rest-sentandwait-adoption.md)  
**Runbook inbound:** [`runbooks/2026-07-10_1800-rest-webhook-adoption.md`](runbooks/2026-07-10_1800-rest-webhook-adoption.md)  
**Runbook AsyncOutbox:** [`runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md`](runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md)  
**Связанные документы:** [`2026-07-04_2338-integration-types-full-report.md`](2026-07-04_2338-integration-types-full-report.md), [`2026-07-06_1519-remaining-backlog-summary.md`](2026-07-06_1519-remaining-backlog-summary.md), [`2026-07-05_1455-integrationflow-full-analysis.md`](2026-07-05_1455-integrationflow-full-analysis.md)

---

## Итог

REST outbound **production-ready**; inbound webhooks **реализованы** (фаза 4); AsyncOutbox HTTP **реализован** (фаза 5):

| Паттерн | Статус | Транспорт |
|---------|--------|-----------|
| **SentAndWait** (sync HTTP request–response) | ✅ фазы 1–2 | `RestHttpTransmitter` |
| **SentAndForgot** (HTTP webhook) | ✅ фаза 3 | `RestPublishTransmitter` |
| **SentAndForgot + outbox** | ✅ фаза 3 | `OutboxRelayService` + `IOutboxTransportResolver` |
| **ReceiveAndProcess webhooks** (inbound) | ✅ фаза 4 | `MapIntegrationFlowWebhook` |
| **AsyncOutbox HTTP** (critical TX + HTTP) | ✅ фаза 5 | `RestRpcPendingPublisher` + callback webhook |

Legacy [`RESTSimpleTransmitter`](../src/IntegrationFlow.Core/Contexts/Integrations/00Samples/Transmitters/RESTSimpleTransmitter.cs) помечен `[Obsolete]`.

---

## Фазы (выполнено)

### Фаза 1 — SentAndWait MVP (P0) ✅

| # | Компонент | Путь |
|---|-----------|------|
| 1.1 | Конфиг + loader | `Rest/Configurations/RestRequestReplyConfiguration*.cs` |
| 1.2 | Shared connections | `RestConnectionProfileResolver`, `RestConnections` в `rest.json` |
| 1.3 | Transmitter | `Rest/SentAndWait/Transmitters/RestHttpTransmitter.cs` |
| 1.4 | Opposite side | `RestSentAndWaitIntegrationOppositeSideBase` |
| 1.5 | DI | `AddIntegrationFlowRest(IConfiguration)` (net8.0) |
| 1.6 | Sample | `00Samples/Rest/SampleRestSentAndWaitProvider.cs` |
| 1.7 | Tracing | `RestTracePropagation` (W3C `traceparent`) |

**Семантика:** at-most-once; 4xx → `Failed`; 5xx/timeout → exception или retry (фаза 2).

### Фаза 2 — Production hardening SentAndWait ✅

| # | Возможность | Реализация |
|---|-------------|------------|
| 2.1 | Retry | Transient 5xx/429; timeout retry через `RetryOnTimeout` + `MessageId` |
| 2.2 | Client cache | `IRestClientResponseCache`, `InMemoryRestClientResponseCache` |
| 2.3 | Auth | Bearer > Basic > ApiKey (`RestHttpAuthentication`) |
| 2.4 | mTLS | `RestHttpClientHandlerFactory` + client cert |
| 2.5 | Health | `RestHealthCheck`, `AddIntegrationFlowRestHealthChecks()` |
| 2.6 | Metrics | `integrationflow.requestreply.*` tag `transport=rest` |
| 2.7 | Runbook | [`2026-07-10_1330-rest-sentandwait-adoption.md`](runbooks/2026-07-10_1330-rest-sentandwait-adoption.md) |

### Фаза 3 — SentAndForgot + outbox ✅

| # | Компонент | Путь |
|---|-----------|------|
| 3.1 | Publish config | `RestPublishConfiguration`, `RestPublishConfigurationLoader` |
| 3.2 | Publish transmitter | `Rest/SentAndForgot/Transmitters/RestPublishTransmitter.cs` |
| 3.3 | Outbox resolver | `IOutboxTransportResolver`, `OutboxTransportResolver` |
| 3.4 | Relay refactor | `OutboxRelayService` — REST или RabbitMQ по имени профиля |
| 3.5 | Non-retryable 4xx | `RestHttpClientErrorException` → abandoned outbox |
| 3.6 | Shared serializer | `IntegrationPayloadSerializer` (outbox + RabbitMQ + REST) |
| 3.7 | Sample | `SampleRestSentAndForgotProvider`, профиль `NotifyWebhook` |

**Выбор транспорта relay:** профиль в `RestPublish` → HTTP; иначе `RabbitMqPublish`. `OutboxId` → header `Idempotency-Key`.

### Фаза 4 — Inbound webhooks ✅

| # | Компонент | Путь |
|---|-----------|------|
| 4.1 | Message model | `Rest/ReceiveAndProcess/Messages/RestWebhookReceivedMessage.cs` |
| 4.2 | Config + loader | `RestWebhookConfiguration`, `RestWebhookConfigurationLoader` (`RestWebhooks`) |
| 4.3 | Processor | `RestWebhookMessageProcessor` (dedup, metrics, tracing) |
| 4.4 | ASP.NET endpoint | `MapIntegrationFlowWebhook` (net8.0) |
| 4.5 | Auth hook | `IRestWebhookAuthenticator` (HMAC — app-level) |
| 4.6 | Tracing | `IntegrationFlowRestActivitySource`, W3C `traceparent` extract |
| 4.7 | Sample | `SampleRestWebhookApplication`, профиль `OrdersInbox` |
| 4.8 | Tests | 22 unit (loader, processor, endpoint) |

**Семантика:** at-least-once; 200 после успеха; 500/503 → partner retry; dedup по `X-Webhook-Id`.

### Фаза 5 — AsyncOutbox HTTP ✅

| # | Компонент | Путь |
|---|-----------|------|
| 5.1 | Request mode | `RestRequestReplyRequestMode.AsyncOutbox` |
| 5.2 | Config | `ResponseWebhookProfileName`, `ResponseCallbackBaseUrl`, `PendingTimeoutSeconds` |
| 5.3 | Transport resolver | `IRpcPendingTransportResolver`, `RpcPendingTransportResolver` |
| 5.4 | Relay refactor | `RpcPendingRelayService` — REST или RabbitMQ |
| 5.5 | REST publisher | `Rest/SentAndWait/RpcPending/RestRpcPendingPublisher.cs` |
| 5.6 | Response correlation | `RestRpcResponseCorrelationProcessor` |
| 5.7 | ASP.NET endpoint | `MapIntegrationFlowRpcResponseWebhook` |
| 5.8 | Sample | `PaymentAuth` + `PaymentRpcResponses` в `rest.json` |
| 5.9 | Tests | unit + WireMock E2E |

**Семантика:** business TX + HTTP request атомарны; partner callback → `CompleteAsync`; reuse `EnqueueRpcRequest` + `SentAndWaitAsyncOutboxIntegration`.

---

## Конфигурация `rest.json`

```json
{
  "RestConnections": {
    "PartnerApi": {
      "BaseAddress": "https://api.partner.example/",
      "BearerToken": ""
    }
  },
  "RestRequestReply": {
    "OrdersLookup": {
      "Connection": "PartnerApi",
      "RequestPath": "/v1/orders/lookup",
      "Method": "POST",
      "ResponseTimeoutSeconds": 15,
      "IdempotencyHeaderName": "Idempotency-Key"
    }
  },
  "RestPublish": {
    "NotifyWebhook": {
      "Connection": "PartnerApi",
      "RequestPath": "/v1/events",
      "Method": "POST",
      "ExpectedStatusCodes": [200, 202, 204]
    }
  }
}
```

Sample: [`src/IntegrationFlow.Core/Contexts/Integrations/00Samples/Rest/rest.json`](../src/IntegrationFlow.Core/Contexts/Integrations/00Samples/Rest/rest.json)

Overlay: `rest.json` → env vars → `IConfiguration` (`RestConfigurationComposition.OverlayConfiguration`).

---

## DI (ASP.NET Core, net8.0)

```csharp
builder.Services.AddIntegrationFlow();
builder.Services.AddIntegrationFlowRest(builder.Configuration);
builder.Services.AddIntegrationFlowRestHealthChecks();          // optional readiness
builder.Services.AddIntegrationFlowOutboxRelay();                // REST + RabbitMQ relay

SentAndWaitIntegrationOptions.ThrowOnFailure = true;
```

---

## API (примеры)

### SentAndWait — sync query к partner API

```csharp
var result = await orgIntegration
    .CreateSentAndWaitIntegration<SampleRestSentAndWaitProvider>(
        oppositeSideCode: "OrdersLookup",
        srcData: lookupRequest)
    .WithMessageId($"order-{orderId}")
    .IntegrateWithResultAsync(cancellationToken);
```

### SentAndForgot + outbox — «БД + webhook»

```csharp
db.EnqueueOutboxMessage("NotifyWebhook", payload);
await db.SaveChangesAsync(cancellationToken);
// OutboxRelayBackgroundService доставит HTTP POST
```

### SentAndForgot — direct publish (без outbox)

```csharp
orgIntegration
    .CreateSentAndForgotIntegration<SampleRestSentAndForgotProvider>(
        oppositeSideCode: "NotifyWebhook",
        srcData: eventPayload)
    .WithMessageId(messageId)
    .Integrate();
```

### AsyncOutbox HTTP — critical TX + partner API

```csharp
builder.Services.AddIntegrationFlowEfRpcPending<MyDbContext>();
builder.Services.AddIntegrationFlowRpcPendingRelay();

app.MapIntegrationFlowRpcResponseWebhook("PaymentAuth");

var pending = db.EnqueueRpcRequest("PaymentAuth", payload);
await db.SaveChangesAsync(cancellationToken);

var result = await orgIntegration
    .CreateSentAndWaitAsyncOutboxIntegration<PaymentAuthProvider>(payload)
    .IntegrateWithResultAsync(pending.Id, cancellationToken);
```

Runbook: [`runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md`](runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md).

---

## Тесты (2026-07-10)

| Область | Файлы | Кол-во (approx.) |
|---------|-------|------------------|
| Config loader | `RestRequestReplyConfigurationLoaderTests.cs` | 5 |
| SentAndWait unit | `RestHttpTransmitterTests.cs`, `RestHttpTransmitterPhase2Tests.cs` | 15 |
| Publish unit | `RestPublishTransmitterTests.cs` | 4 |
| Outbox | `OutboxTransportResolverTests.cs`, `OutboxRelayServiceRestTests.cs` | 5 |
| Webhooks | `RestWebhook*Tests.cs` | 22 |
| AsyncOutbox | `RestRpcResponseCorrelationProcessorTests.cs`, `RpcPendingTransportResolverTests.cs` | 7 |
| OpenTelemetry | `RecordRequestReply_RecordsTransportTag` | 1 |
| Integration WireMock | `RestHttpEndToEndTests.cs`, `RestOutboxRelayEndToEndTests.cs`, `RestRpcPendingEndToEndTests.cs` | 7 |

**Итого REST:** ~66 тестов (unit + integration). Полный suite: **~240 unit** + integration (см. CI).

```bash
dotnet test IntegrationFlow.sln --filter "FullyQualifiedName~Rest"
dotnet test IntegrationFlow.sln --filter "Category!=Integration"
```

---

## Архитектура (целевая vs факт)

```
Rest/
├── Configurations/     # RequestReply, Publish, Webhooks, Connection profiles
├── Connections/        # HttpClient provider, handler factory, connections
├── Auth/               # Bearer, Basic, ApiKey
├── SentAndWait/        # RestHttpTransmitter, RpcPending/RestRpcPendingPublisher
├── SentAndForgot/      # RestPublishTransmitter, opposite side base
├── ReceiveAndProcess/  # RestWebhookMessageProcessor, RestRpcResponseCorrelationProcessor
├── SentAndWait/Cache/  # IRestClientResponseCache (фаза 2)
├── Health/             # RestHealthCheck (net8.0)
├── Tracing/            # traceparent inject/extract
└── Exceptions/         # RestHttpException, RestHttpClientErrorException

RpcPending/
├── RpcPendingRelayService.cs       # transport-agnostic relay (REST + RabbitMQ)
├── RpcPendingTransportResolver.cs
├── IRpcPendingTransportResolver.cs

Outbox/
├── OutboxRelayService.cs       # transport-agnostic outbox relay
├── OutboxTransportResolver.cs  # RestPublish → REST, else RabbitMqPublish

01Infrastructure/
└── IntegrationPayloadSerializer.cs
```

---

## Риски (актуальные)

| ID | Риск | Severity | Mitigation |
|----|------|----------|------------|
| H1 | HTTP timeout = unknown state (sync SentAndWait) | Высокий | `Idempotency-Key` + partner idempotency; critical → **REST AsyncOutbox** (фаза 5) |
| H5 | Secrets в `rest.json` | Средний | Env overlay — runbook |
| H4 | 4xx retry loop | Закрыт | Publish 4xx → abandoned; SentAndWait 4xx → Failed без retry |
| — | OAuth2 refresh | Open | Фаза 2+ / app-level |
| — | Inbound webhook security | Частично | `IRestWebhookAuthenticator` hook; HMAC — app-level |

Полный список: [`plans/2026-07-10_0853-rest-implementation.md`](plans/2026-07-10_0853-rest-implementation.md) §6.

---

## Backlog (следующие фазы)

| Фаза | Scope | Effort | План |
|------|-------|--------|------|
| v1.1 | OAuth2 token refresh, Polly policies, polling response | optional | out of scope v1 |

---

## Документация (индекс REST)

| Документ | Назначение |
|----------|------------|
| [`plans/2026-07-10_0853-rest-implementation.md`](plans/2026-07-10_0853-rest-implementation.md) | Полный план фаз 0–5 |
| [`runbooks/2026-07-10_1330-rest-sentandwait-adoption.md`](runbooks/2026-07-10_1330-rest-sentandwait-adoption.md) | Production adoption (SentAndWait + outbox) |
| [`runbooks/2026-07-10_1800-rest-webhook-adoption.md`](runbooks/2026-07-10_1800-rest-webhook-adoption.md) | Inbound webhooks (ReceiveAndProcess) |
| [`runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md`](runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md) | AsyncOutbox HTTP (critical TX) |
| [`2026-07-06_1519-remaining-backlog-summary.md`](2026-07-06_1519-remaining-backlog-summary.md) | Общий backlog проекта |
| [`README.md`](../README.md) | Quick start REST в корне репозитория |

---

## Changelog статуса

| Дата | Изменение |
|------|-----------|
| 2026-07-10 22:10 | Документация: runbook AsyncOutbox, тесты, архитектура |
| 2026-07-10 21:52 | Фаза 5 ✅: AsyncOutbox HTTP, callback webhook |
| 2026-07-10 18:00 | Фаза 4 ✅: inbound webhooks, runbook |
| 2026-07-10 15:07 | Создан документ; зафиксированы фазы 1–3 ✅ |
| 2026-07-10 13:30 | Фаза 2: runbook, hardening |
| 2026-07-10 09:00 | Фаза 1: SentAndWait MVP |
