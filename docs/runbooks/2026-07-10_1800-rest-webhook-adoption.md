# Runbook: REST inbound webhooks (ReceiveAndProcess)

**Статус:** актуально  
**Создан:** 2026-07-10 18:00 (UTC+3)  
**План:** [`plans/2026-07-10_0853-rest-implementation.md`](../plans/2026-07-10_0853-rest-implementation.md) § фаза 4  
**Статус REST:** [`2026-07-10_1507-rest-implementation-status.md`](../2026-07-10_1507-rest-implementation-status.md)

---

## Когда использовать

Inbound webhooks от партнёров (push HTTP POST), когда **нет** очереди сообщений. Каркас не использует `ListenerBase` — endpoint регистрируется в ASP.NET Core.

| Сценарий | Паттерн |
|----------|---------|
| Партнёр шлёт POST на ваш URL | `MapIntegrationFlowWebhook` |
| Идемпотентность при retry партнёра | `IMessageDeduplicationStore` + header `X-Webhook-Id` |
| Outbound уведомление партнёру | `RestPublishTransmitter` (фаза 3) |
| Critical TX + HTTP к партнёру | [`REST AsyncOutbox runbook`](2026-07-10_2130-rest-asyncoutbox-adoption.md) |

---

## Быстрый старт

### 1. Конфигурация `rest.json`

```json
{
  "RestWebhooks": {
    "OrdersInbox": {
      "Path": "/integrations/webhooks/orders",
      "MessageIdHeaderName": "X-Webhook-Id",
      "CorrelationIdHeaderName": "X-Correlation-Id",
      "MaxBodyBytes": 1048576,
      "AllowedMethods": ["POST"],
      "RequireMessageId": false
    }
  }
}
```

### 2. DI + endpoint (net8.0)

```csharp
builder.Services.AddIntegrationFlowRest(builder.Configuration);
builder.Services.AddSingleton<IIntegrationLogger, YourIntegrationLogger>();

// Опционально: dedup (in-memory sample или EF)
builder.Services.AddSingleton<IMessageDeduplicationStore, InMemoryMessageDeduplicationStore>();

// Опционально: HMAC / API key verification
builder.Services.AddSingleton<IRestWebhookAuthenticator, YourWebhookAuthenticator>();

var app = builder.Build();

app.MapIntegrationFlowWebhook(
    "OrdersInbox",
    "/integrations/webhooks/orders",
    async (RestWebhookReceivedMessage message, CancellationToken cancellationToken) =>
    {
        // Business logic: parse message.BodyText, persist, etc.
    });
```

### 3. OpenTelemetry tracing

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(IntegrationFlowRestActivitySource.Name));
```

---

## Семантика доставки

| HTTP ответ | Когда | Поведение партнёра |
|------------|-------|-------------------|
| **200** | Handler успешен или dedup skip | Stop retry |
| **400** | `RequireMessageId=true`, header отсутствует | Fix payload |
| **401** | `IRestWebhookAuthenticator` вернул false | Fix auth |
| **413** | Body > `MaxBodyBytes` | Fix payload |
| **500** | Handler exception | Retry (at-least-once) |
| **503** | Параллельная обработка того же MessageId | Retry позже |

**200 после успеха** — аналог manual ack в RabbitMQ listener.

---

## Dedup

1. Зарегистрируйте `IMessageDeduplicationStore` (in-memory для dev, `AddIntegrationFlowEfDeduplication` для prod).
2. Партнёр должен слать стабильный id в header (`X-Webhook-Id` по умолчанию).
3. При `RequireMessageId=true` запросы без header → **400**.

Повторная доставка с тем же MessageId после успешной обработки → **200** без повторного вызова handler (`dedup_skip` в metrics).

---

## Безопасность (v1)

Встроенной HMAC verification **нет**. Реализуйте через `IRestWebhookAuthenticator`:

```csharp
public sealed class HmacWebhookAuthenticator : IRestWebhookAuthenticator
{
    public Task<bool> TryAuthenticateAsync(
        HttpContext httpContext,
        RestWebhookConfiguration configuration,
        RestWebhookReceivedMessage message,
        CancellationToken cancellationToken)
    {
        // Verify X-Signature against message.Body + shared secret
        return Task.FromResult(/* true or false */);
    }
}
```

Rate limiting — на уровне ASP.NET middleware (не в каркасе v1).

---

## Observability

| Signal | Hook |
|--------|------|
| Duration / success | `IIntegrationFlowMetrics.RecordMessageProcessed(profile, duration, success)` |
| Dedup / retry | `RecordConsumerOutcome(profile, ConsumerOutcomeReason.*)` |
| Tracing | W3C `traceparent` из входящего HTTP → `IntegrationFlow.Rest` ActivitySource |

---

## Production checklist

- [ ] `rest.json` + env overlay для secrets (не в git)
- [ ] `IMessageDeduplicationStore` (EF для multi-instance)
- [ ] `IRestWebhookAuthenticator` (HMAC или mTLS на reverse proxy)
- [ ] `RequireMessageId=true` если партнёр поддерживает id
- [ ] `MaxBodyBytes` согласован с reverse proxy limit
- [ ] Alerts на рост `500`/`503` по profile
- [ ] `AddSource(IntegrationFlowRestActivitySource.Name)` в OTel

---

## Связанные документы

| Документ | Назначение |
|----------|------------|
| [`2026-07-10_1330-rest-sentandwait-adoption.md`](2026-07-10_1330-rest-sentandwait-adoption.md) | Outbound REST + outbox |
| [`2026-07-04_2130-production-adoption.md`](2026-07-04_2130-production-adoption.md) | RabbitMQ ReceiveAndProcess (dedup паттерн) |
