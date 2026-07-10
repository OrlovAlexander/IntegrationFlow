# IntegrationFlow

Библиотека для построения интеграций между системами на .NET. Единый каркас для трёх сценариев обмена данными и готовая реализация транспортов **RabbitMQ** и **REST** (outbound + inbound webhooks).

## Типы интеграций

| Сценарий | Когда использовать | Направление | Конфигурация RabbitMQ |
|----------|-------------------|-------------|------------------------|
| **ReceiveAndProcess** | Обработка входящих сообщений из очереди | Consumer | `RabbitMq` |
| **SentAndForgot** | Отправка события без ожидания ответа | Producer | `RabbitMqPublish` |
| **SentAndWait** | Запрос–ответ (RPC) с ожиданием результата | Producer + consumer ответа | `RabbitMqRequestReply` |
| **SentAndWait (REST)** | HTTP request–response к внешнему API | `ITransmitterAsync` | `RestRequestReply` в `rest.json` |
| **SentAndForgot (REST)** | HTTP POST webhook / callback (+ outbox) | `ITransmitterWithResult` | `RestPublish` в `rest.json` |
| **ReceiveAndProcess (REST)** | Inbound webhook от партнёра (push HTTP) | ASP.NET endpoint | `RestWebhooks` в `rest.json` |
| **SentAndWait AsyncOutbox (REST)** | Critical TX + HTTP к партнёру + callback | RpcPending + webhook | `RestRequestReply` (`RequestMode: AsyncOutbox`) |

Каждый сценарий расширяется через интерфейсы: конфигурация, подключение, форматирование, валидация, логирование.

## Возможности по типам интеграции

### ReceiveAndProcess — входящие сообщения

| Возможность | Описание |
|-------------|----------|
| Manual ack | Подтверждение **после** завершения обработки |
| Reconnect | Автоматическое переподключение при разрыве (backoff 1–30 с) |
| Graceful shutdown | Ожидание in-flight обработок, nack requeue при остановке |
| Идемпотентность | `IMessageDeduplicationStore` — пропуск дубликатов по `MessageId` |
| DLQ | `MaxRetryCount` + `x-dead-letter-exchange` на брокере |
| Hosted listener | `AddIntegrationFlowRabbitMqListener` (netstandard2.0, net8.0) |
| TLS | AMQPS через `SslEnabled` / `SslServerName` |
| AMQP headers | `RabbitMqReceivedMessage.Headers`, `RabbitMqMessageHeaders.TryGetString` |
| Consumer tag / exclusive | `ConsumerTag`, `Exclusive` для `basic.consume` (SAC) |
| Priority / TTL | `Priority`, `ExpirationMilliseconds` в publish/RPC конфиге |

### SentAndForgot — исходящие сообщения

| Возможность | Описание |
|-------------|----------|
| Publisher confirms | Подтверждение доставки от брокера (по умолчанию включено) |
| Прямая публикация | `Integrate()` / `IntegrateWithResult()` в очередь или exchange |
| Transactional outbox | Атомарная запись «БД + сообщение» через outbox relay |
| EF Core store | `IntegrationFlow.EntityFrameworkCore` — production outbox и dedup |
| Connection pool | `ReuseConnection` для high-frequency publish |
| TLS | AMQPS |

### SentAndWait — запрос–ответ (RPC)

| Возможность | Описание |
|-------------|----------|
| Синхронный RPC | `ReplyTo` + `CorrelationId`, по умолчанию `DirectReplyTo` |
| Асинхронный API | `IntegrateAsync()` / `IntegrateWithResultAsync()` для ASP.NET |
| Идемпотентный RPC | `MessageId` + `RabbitMqRpcServerPipeline` + retry после timeout |
| AsyncOutbox RPC | Критичные запросы: staging в БД → relay → correlation ответа |
| Server handler | `AddIntegrationFlowRabbitMqRpcServer` — reply до ack |
| Connection reuse | `ReuseConnection`, `ReuseReplyConnection` |
| Publisher confirms | Confirm на publish request |

## NuGet-пакеты

| Пакет | Назначение |
|-------|------------|
| `IntegrationFlow.Core` | Каркас интеграций, RabbitMQ transport (net8.0, netstandard2.0) |
| `IntegrationFlow.EntityFrameworkCore` | EF outbox, dedup, AsyncOutbox RPC stores (net8.0) |
| `IntegrationFlow.Metrics.OpenTelemetry` | Метрики через `System.Diagnostics.Metrics` (net8.0) |

```bash
dotnet pack IntegrationFlow.sln -c Release
```

Release process: [`docs/runbooks/2026-07-04_0845-nuget-release.md`](docs/runbooks/2026-07-04_0845-nuget-release.md).

## Быстрый старт

```csharp
using IntegrationFlow.DependencyInjection;

services.AddIntegrationFlow();
```

`AddIntegrationFlow` регистрирует базовые сервисы и локализацию из resx-ресурсов.

## RabbitMQ: общая конфигурация

Настройки — в `rabbitmq.json` рядом с приложением. Поддерживаются **именованные профили** и legacy flat-формат.

**Приоритет источников** (от низкого к высокому): `rabbitmq.json` → environment variables → `IConfiguration` (appsettings, user secrets).

```bash
# Linux / Docker / Kubernetes
export RabbitMq__Inbox__HostName=rabbit.prod.internal
export RabbitMq__Inbox__Password=secret
export RabbitMqPublish__OrdersOut__HostName=rabbit.prod.internal
export RabbitMqRequestReply__OrdersRpc__Password=secret
```

```csharp
var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    services.AddIntegrationFlow();
    services.AddIntegrationFlowRabbitMq(context.Configuration);
});
```

**Именованные профили** (рекомендуется):

```json
{
  "RabbitMqConnections": {
    "Prod": {
      "HostName": "rabbit.prod.internal",
      "Port": 5671,
      "UserName": "integration",
      "Password": "secret",
      "VirtualHost": "/prod",
      "SslEnabled": true,
      "SslServerName": "rabbit.prod.internal"
    }
  },
  "RabbitMq": {
    "Inbox": {
      "Connection": "Prod",
      "QueueName": "integration.inbox",
      "PrefetchCount": 1,
      "ClientProvidedName": "IntegrationFlow.InboxListener"
    }
  },
  "RabbitMqPublish": {
    "OrdersOut": {
      "Connection": "Prod",
      "PublishTarget": "Queue",
      "QueueName": "orders.outbox"
    }
  },
  "RabbitMqRequestReply": {
    "OrdersRpc": {
      "Connection": "Prod",
      "QueueName": "orders.rpc"
    }
  }
}
```

Секция `RabbitMqConnections` хранит общие параметры TCP-подключения (`HostName`, `Port`, `UserName`, `Password`, `VirtualHost`, TLS). В профилях сценариев укажите `"Connection": "Prod"` — дублировать учётные данные в трёх секциях не нужно. Параметры профиля сценария **переопределяют** общий профиль (например, свой `HostName` или `ClientProvidedName`).

Environment variables для shared profile:

```bash
export RabbitMqConnections__Prod__HostName=rabbit.prod.internal
export RabbitMqConnections__Prod__Password=secret
```

**Без shared profile** (полная inline-конфигурация) по-прежнему поддерживается:

```json
{
  "RabbitMq": {
    "Inbox": {
      "HostName": "localhost",
      "QueueName": "integration.inbox",
      "PrefetchCount": 1
    }
  },
  "RabbitMqPublish": {
    "OrdersOut": {
      "HostName": "localhost",
      "PublishTarget": "Queue",
      "QueueName": "orders.outbox"
    }
  },
  "RabbitMqRequestReply": {
    "OrdersRpc": {
      "HostName": "localhost",
      "QueueName": "orders.rpc"
    }
  }
}
```

**AMQPS (TLS):** `SslEnabled: true`, `SslServerName`, порт `5671` — для всех трёх секций конфигурации.

Подробнее о секретах и overlay: разделы ниже и [`docs/runbooks/2026-07-04_2130-production-adoption.md`](docs/runbooks/2026-07-04_2130-production-adoption.md).

---

## RabbitMQ: ReceiveAndProcess

Consumer для входящих сообщений. Работает асинхронно, потокобезопасно, с manual ack.

### Запуск (netstandard2.0 и .NET 8+)

```csharp
using IntegrationFlow.DependencyInjection;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddIntegrationFlow();
        services.AddIntegrationFlowRabbitMqListener("Inbox", message =>
        {
            // обработка входящего сообщения
        });
    })
    .Build();

await host.RunAsync();
```

### Параметры consumer (`RabbitMq`)

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| `QueueName` | — | Очередь (обязательно; `QueueDeclarePassive`) |
| `PrefetchCount` | `1` | Неподтверждённые сообщения на consumer |
| `RequeueOnFailure` | `false` | Requeue при ошибке обработки |
| `MaxRetryCount` | `0` | Лимит попыток (0 = без лимита); после — nack без requeue → DLQ |
| `AutomaticRecoveryEnabled` | `true` | Client-level recovery + reconnect loop каркаса |
| `ValidateTopology` | `true` | Passive declare очереди перед consume |
| `DeclareTopologyOnStartup` | `false` | Active `QueueDeclare` (только dev; см. ниже) |
| `ConsumerTag` | `""` | Тег `basic.consume`; пусто — брокер сгенерирует |
| `Exclusive` | `false` | Exclusive consumer (single-active на classic queue) |
| `SslEnabled` / `SslServerName` | `false` / — | AMQPS |

> **Single-active-consumer:** для classic queue задайте `Exclusive: true` только если нужен один consumer на соединение. Для кластерного failover предпочтительнее [politica SAC](https://www.rabbitmq.com/docs/consumers#single-active-consumer) на брокере; `Exclusive` — простой вариант для dev/одной ноды.

> **Production:** оставляйте `DeclareTopologyOnStartup = false` и создавайте очереди/DLQ через IaC (Terraform, RabbitMQ definitions). Active declare не задаёт DLQ, TTL и bindings. Runbook: [`docs/runbooks/2026-07-07_2137-rabbitmq-topology-adoption.md`](docs/runbooks/2026-07-07_2137-rabbitmq-topology-adoption.md).

### AMQP headers

В обработчике `message` — `RabbitMqReceivedMessage` с read-only `Headers`:

```csharp
services.AddIntegrationFlowRabbitMqListener("Inbox", message =>
{
    if (message is RabbitMqReceivedMessage received)
    {
        if (RabbitMqMessageHeaders.TryGetString(received.Headers, "x-tenant-id", out var tenantId))
        {
            // маршрутизация по tenant
        }

        var deathCount = RabbitMqMessageHeaders.GetDeathCount(received.Headers);
    }
});
```

Пример: `SampleRabbitMqHeaderAwareReceiveAndProcessHandler`. Distributed tracing (`traceparent`) читается автоматически каркасом; headers доступны и для custom logic.

### Идемпотентность

Реализуйте `IMessageDeduplicationStore` (sample: `InMemoryMessageDeduplicationStore`, production: EF store). При повторной доставке того же `MessageId` — пропуск или отложенный requeue.

### Legacy launcher (netstandard2.0)

`IReceiveAndProcessLauncher` + `BeginReceiving()` сохранён для обратной совместимости, помечен `[Obsolete]` на net8.0.

---

## RabbitMQ: SentAndForgot

Публикация в очередь/exchange без ожидания ответа.

```csharp
var integration = orgIntegration.CreateSentAndForgotIntegration<SampleRabbitMqSentAndForgotProvider>(
    oppositeSideCode: "OrdersOut",
    srcData: orderEvent);

var result = integration.IntegrateWithResult();
if (!result.Success)
{
    // обработать ошибку publish
}

// Production: бросать исключение при ошибке
SentAndForgotIntegrationOptions.ThrowOnFailure = true;
integration.Integrate();
```

### Параметры publish (`RabbitMqPublish`)

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| `PublisherConfirmsEnabled` | `true` | Ждать confirm от брокера |
| `ConfirmTimeoutSeconds` | `30` | Таймаут confirm |
| `Persistent` | `true` | `DeliveryMode=2` |
| `Priority` | — | AMQP priority (0–255); не задано — не выставляется |
| `ExpirationMilliseconds` | — | Per-message TTL (мс); не задано — не выставляется |
| `Mandatory` | `false` | Ошибка, если сообщение не маршрутизируется |
| `ReuseConnection` | `false` | Переиспользование TCP |
| `ValidateTopology` | `true` | Passive declare перед publish |
| `DeclareTopologyOnStartup` | `false` | Active declare queue/exchange (только dev) |
| `ExchangeType` | `direct` | Тип exchange при active declare |
| `SslEnabled` / `SslServerName` | `false` / — | AMQPS |

### Transactional Outbox

Для атомарности «БД + сообщение» — outbox в той же транзакции, что и бизнес-данные:

```csharp
services.AddIntegrationFlow();
services.AddIntegrationFlowOutboxRelay(options =>
{
    options.BatchSize = 20;
    options.PollingInterval = TimeSpan.FromSeconds(5);
    options.MaxAttempts = 10;
});

// EF Core (production):
services.AddIntegrationFlowEfOutbox<MyDbContext>();
services.AddIntegrationFlowEfDeduplication<MyDbContext>();
```

Пример: [`docs/examples/2026-07-03_1639-ef-outbox-transaction.md`](docs/examples/2026-07-03_1639-ef-outbox-transaction.md).

> Прямая публикация **после** commit БД — антипаттерн. Используйте outbox в той же TX.

---

## RabbitMQ: SentAndWait

Request–response через RabbitMQ.

### End-to-end: hosted RPC server + client

**1. Конфигурация** (`rabbitmq.json`) — один профиль в `RabbitMqRequestReply`:

```json
{
  "RabbitMqRequestReply": {
    "OrdersRpc": {
      "HostName": "localhost",
      "QueueName": "orders.rpc.requests",
      "RequestTarget": "Queue",
      "ReplyMode": "DirectReplyTo",
      "ResponseTimeoutSeconds": 30
    }
  }
}
```

Очередь `orders.rpc.requests` должна существовать на брокере.

**2. RPC server** (Worker / `IHost`, .NET 8+):

```csharp
using IntegrationFlow.DependencyInjection;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddIntegrationFlow();
        services.AddIntegrationFlowRabbitMqRpcServer("OrdersRpc");
        // Кастомный ответ:
        // services.AddIntegrationFlowRabbitMqRpcServer("OrdersRpc",
        //     buildResponseAsync: request => Task.FromResult($"{{\"echo\":{request.BodyText}}}"));
    })
    .Build();

await host.RunAsync();
```

`AddIntegrationFlowRabbitMqRpcServer` слушает request-очередь из профиля `RabbitMqRequestReply` и публикует reply **до** consumer ack через `RabbitMqRpcServerPipeline`.

**3. RPC client** (другой процесс или тот же host без listener):

```csharp
using IntegrationFlow.DependencyInjection;

services.AddIntegrationFlow();

SentAndWaitIntegrationOptions.ThrowOnFailure = true;

var integration = orgIntegration.CreateSentAndWaitIntegration<SampleRabbitMqSentAndWaitProvider>(
    oppositeSideCode: "OrdersRpc",
    srcData: new { OrderId = 42 });

var handler = orgIntegration.GetSentAndWaitResultHandler<SampleRabbitMqSentAndWaitProvider>("OrdersRpc");
await integration.IntegrateAsync(handler, cancellationToken);
```

Полный sample: `SampleHostedRabbitMqSentAndWaitRpcApplication` в `00Samples/SentAndWait`.

### Синхронный RPC (клиент)

```csharp
services.AddIntegrationFlow();

var integration = orgIntegration.CreateSentAndWaitIntegration<SampleRabbitMqSentAndWaitProvider>(
    oppositeSideCode: "OrdersRpc",
    srcData: new { OrderId = 42 });

var handler = orgIntegration.GetSentAndWaitResultHandler<SampleRabbitMqSentAndWaitProvider>("OrdersRpc");
await integration.IntegrateAsync(handler, cancellationToken);
```

### Server-side handler (низкоуровневый)

Для кастомной логики используйте `RabbitMqRpcServerInboxMessageProcessing` или `AddIntegrationFlowRabbitMqRpcServer` с `buildResponseAsync`.

### Идемпотентный sync RPC

Задайте `MessageId` через `WithMessageId()`; на server — `IRequestReplyResponseStore` + `RabbitMqRpcServerPipeline`. При timeout: `SentAndWaitIntegrationOptions.RetryOnTimeout = true`.

### AsyncOutbox RPC (критичные запросы)

`RequestMode: AsyncOutbox` + `ResponseQueueName` в конфиге:

- Stage: `CreateSentAndWaitAsyncOutboxIntegration` → `CreatePendingRequest()` в TX
- Relay: `AddIntegrationFlowRpcPendingRelay()`
- Correlation: `AddIntegrationFlowRabbitMqRpcResponseCorrelation()`
- EF: `AddIntegrationFlowEfRpcPending<TContext>()`

Runbook: [`docs/runbooks/2026-07-04_2130-sentandwait-rpc-adoption.md`](docs/runbooks/2026-07-04_2130-sentandwait-rpc-adoption.md).

### Параметры RPC (`RabbitMqRequestReply`)

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| `MaxConcurrentRequests` | `1` | Лимит параллельных запросов (`0` = без лимита) |
| `ReuseConnection` | `false` | Переиспользование TCP (client) |
| `ReuseReplyConnection` | `true` | Pool channel для server-side reply |
| `ResponseTimeoutSeconds` | — | Таймаут ожидания ответа |
| `PublisherConfirmsEnabled` | `true` | Confirm на publish request |
| `ValidateTopology` | `true` | Passive declare перед RPC publish |
| `DeclareTopologyOnStartup` | `false` | Active declare queue/exchange (только dev) |
| `ExchangeType` | `direct` | Тип exchange при active declare |
| `Priority` | — | AMQP priority для request (0–255) |
| `ExpirationMilliseconds` | — | Per-message TTL request (мс) |
| `SslEnabled` / `SslServerName` | `false` / — | AMQPS |

### Production defaults

```csharp
SentAndWaitIntegrationOptions.ThrowOnFailure = true;
SentAndForgotIntegrationOptions.ThrowOnFailure = true;
```

---

## REST: SentAndWait (HTTP client)

Синхронный HTTP request–response к внешнему API. Конфигурация в `rest.json` (секция `RestRequestReply`).

```csharp
using IntegrationFlow.DependencyInjection;

builder.Services.AddIntegrationFlow();
builder.Services.AddIntegrationFlowRest(builder.Configuration);

SentAndWaitIntegrationOptions.ThrowOnFailure = true;

var integration = orgIntegration.CreateSentAndWaitIntegration<SampleRestSentAndWaitProvider>(
    oppositeSideCode: "OrdersLookup",
    srcData: lookupRequest)
    .WithMessageId($"order-{orderId}");

var result = await integration.IntegrateWithResultAsync(cancellationToken);
```

Пример `rest.json`:

```json
{
  "RestConnections": {
    "PartnerApi": {
      "BaseAddress": "https://api.partner.com/",
      "BearerToken": "from-secrets"
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
  }
}
```

`TransmitData.MessageId` → HTTP header `Idempotency-Key` (имя настраивается). Legacy [`RESTSimpleTransmitter`](src/IntegrationFlow.Core/Contexts/Integrations/00Samples/Transmitters/RESTSimpleTransmitter.cs) помечен `[Obsolete]`.

Production hardening (retry, auth, cache, health, metrics): [`docs/runbooks/2026-07-10_1330-rest-sentandwait-adoption.md`](docs/runbooks/2026-07-10_1330-rest-sentandwait-adoption.md).

### REST: SentAndForgot (HTTP webhook + outbox)

Публикация HTTP webhook через outbox relay. Профиль в секции `RestPublish` файла `rest.json`:

```json
"RestPublish": {
  "NotifyWebhook": {
    "Connection": "PartnerApi",
    "RequestPath": "/v1/events",
    "Method": "POST",
    "ExpectedStatusCodes": [200, 202, 204]
  }
}
```

```csharp
builder.Services.AddIntegrationFlowRest(builder.Configuration);
builder.Services.AddIntegrationFlowOutboxRelay();

// В business TX:
db.EnqueueOutboxMessage("NotifyWebhook", payload);
await db.SaveChangesAsync(cancellationToken);
```

`OutboxRelayService` выбирает транспорт по имени профиля: `RestPublish` → HTTP, иначе `RabbitMqPublish`.

### REST: ReceiveAndProcess (inbound webhooks, net8.0)

Приём push-webhook от партнёра через ASP.NET endpoint (не `ListenerBase`). Профиль в секции `RestWebhooks`:

```json
"RestWebhooks": {
  "OrdersInbox": {
    "Path": "/integrations/webhooks/orders",
    "MessageIdHeaderName": "X-Webhook-Id",
    "MaxBodyBytes": 1048576
  }
}
```

```csharp
builder.Services.AddIntegrationFlowRest(builder.Configuration);
builder.Services.AddSingleton<IMessageDeduplicationStore, InMemoryMessageDeduplicationStore>();

app.MapIntegrationFlowWebhook(
    "OrdersInbox",
    "/integrations/webhooks/orders",
    async (RestWebhookReceivedMessage message, CancellationToken cancellationToken) =>
    {
        // Business handler
    });
```

Семантика: **200 после успеха** (аналог ack); **500/503** → partner retry; dedup по `X-Webhook-Id`. Runbook: [`docs/runbooks/2026-07-10_1800-rest-webhook-adoption.md`](docs/runbooks/2026-07-10_1800-rest-webhook-adoption.md).

### REST: AsyncOutbox HTTP (critical TX, net8.0)

Для сценариев «commit БД + HTTP-запрос к партнёру» атомарно. Partner принимает async (202) и POST callback.

```json
"RestRequestReply": {
  "PaymentAuth": {
    "Connection": "PartnerApi",
    "RequestPath": "/v1/payments/authorize",
    "RequestMode": "AsyncOutbox",
    "ResponseWebhookProfileName": "PaymentRpcResponses",
    "ResponseCallbackBaseUrl": "https://app.example.com",
    "PendingTimeoutSeconds": 300
  }
},
"RestWebhooks": {
  "PaymentRpcResponses": {
    "Path": "/integrations/rpc-responses/payments"
  }
}
```

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

Runbook: [`docs/runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md`](docs/runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md).

План и статус REST: [`docs/plans/2026-07-10_0853-rest-implementation.md`](docs/plans/2026-07-10_0853-rest-implementation.md), [`docs/2026-07-10_1507-rest-implementation-status.md`](docs/2026-07-10_1507-rest-implementation-status.md).

---

## Наблюдаемость

Пакет `IntegrationFlow.Metrics.OpenTelemetry` + встроенные health checks, structured logging и distributed tracing.

| Возможность | Что даёт | Регистрация |
|-------------|----------|-------------|
| Метрики | Throughput, latency, outbox backlog, RPC, reconnect | `AddIntegrationFlowOpenTelemetryMetrics()` |
| Health checks | Readiness listener, outbox relay, RPC correlation | `AddIntegrationFlowRabbitMqHealthChecks()` |
| Structured logging | `profile`, `message_id`, `correlation_id`, `outcome` в логах | `AddIntegrationFlow()` + host `ILogger` |
| Distributed tracing | W3C `traceparent` через AMQP headers | `AddSource(IntegrationFlowRabbitMqActivitySource.Name)` |
| Broker connectivity | Gauge подключения к брокеру по профилю | Автоматически с метриками |

### Production setup (.NET 8+)

```csharp
using IntegrationFlow.DependencyInjection;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;
using IntegrationFlow.Metrics.OpenTelemetry.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIntegrationFlow();
builder.Services.AddIntegrationFlowRabbitMq(builder.Configuration);
builder.Services.AddIntegrationFlowRabbitMqListener("Inbox", message => { /* ... */ });
builder.Services.AddIntegrationFlowOutboxRelay();
builder.Services.AddIntegrationFlowOpenTelemetryMetrics();
builder.Services.AddIntegrationFlowRabbitMqHealthChecks(options =>
{
    options.MaxReconnectAttemptsBeforeUnhealthy = 5;
    options.OutboxRelayMaxConsecutiveFailures = 5;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("IntegrationFlow").AddPrometheusExporter())
    .WithTracing(t => t
        .AddSource(IntegrationFlowRabbitMqActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapPrometheusScrapingEndpoint();
```

### Health checks

| Check | Имя | Unhealthy когда |
|-------|-----|-----------------|
| Listener | `integrationflow.rabbitmq.listener` | Нет соединения или reconnect ≥ порога |
| Outbox relay | `integrationflow.rabbitmq.outbox_relay` | Подряд ≥ N неудачных relay batch |
| RPC correlation | `integrationflow.rabbitmq.rpc_correlation` | Response consumer отключён |

Если компонент не зарегистрирован в DI, check возвращает **Healthy** (no-op).

### Метрики

| Имя | Тип | Описание |
|-----|-----|----------|
| `integrationflow.message.processed` | Counter | Обработанные inbox-сообщения |
| `integrationflow.message.processing.duration` | Histogram | Длительность обработки |
| `integrationflow.message.consumer.outcome` | Counter | Исходы ack/nack/requeue/dedup (`reason`) |
| `integrationflow.outbox.pending` | Gauge | Backlog outbox |
| `integrationflow.outbox.relay.*` | Counter | Published / failed / abandoned |
| `integrationflow.requestreply.*` | Counter/Histogram | RPC round-trip |
| `integrationflow.rpc.pending.*` | Counter/Gauge | AsyncOutbox RPC |
| `integrationflow.broker.connected` | Gauge | Подключение к брокеру (1/0) |
| `integrationflow.listener.reconnect` | Counter | Переподключения listener |

Полный список и PromQL-алерты: [`docs/runbooks/2026-07-04_0845-metrics-and-alerting.md`](docs/runbooks/2026-07-04_0845-metrics-and-alerting.md).

### Structured logging

Поля в log scope (Serilog, OTel logs, JSON console):

| Поле | Описание |
|------|----------|
| `integrationflow.profile` | Имя профиля RabbitMQ |
| `integrationflow.message_id` | AMQP `MessageId` |
| `integrationflow.correlation_id` | AMQP `CorrelationId` |
| `integrationflow.delivery_tag` | Delivery tag consumer |
| `integrationflow.kind` | `listener`, `outbox_relay`, `publish`, `request_reply`, … |
| `integrationflow.outcome` | `ack`, `nack`, `requeue`, `dedup_skip`, `published`, … |

### Distributed tracing

Spans: `rabbitmq.publish`, `rabbitmq.receive`, `rabbitmq.request`, `rabbitmq.reply`, `rabbitmq.response`.

Сквозной trace: HTTP → publish → consume (другой сервис) → handler → reply → response.

> Извлечение parent context (`ActivityContext.TryParse`) — на **net8.0**. На netstandard2.0 inject работает, extract — best-effort.

---

## Сборка и тесты

```bash
dotnet build IntegrationFlow.sln
dotnet test IntegrationFlow.sln --filter "Category!=Integration"
dotnet test IntegrationFlow.sln --filter "Category=Integration"
```

Integration-тесты требуют Docker (Testcontainers).

## Структура проекта

```
IntegrationFlow/
├── src/IntegrationFlow.Core/               # Каркас + RabbitMQ (net8.0, netstandard2.0)
├── src/IntegrationFlow.EntityFrameworkCore/  # EF stores (net8.0)
├── src/IntegrationFlow.Metrics.OpenTelemetry/
└── tests/
```

## Документация

| Тема | Документ |
|------|----------|
| Production adoption | [`docs/runbooks/2026-07-04_2130-production-adoption.md`](docs/runbooks/2026-07-04_2130-production-adoption.md) |
| DLQ topology | [`docs/runbooks/2026-07-07_2137-rabbitmq-dlq-topology.md`](docs/runbooks/2026-07-07_2137-rabbitmq-dlq-topology.md) |
| Topology adoption (P4-6) | [`docs/runbooks/2026-07-07_2137-rabbitmq-topology-adoption.md`](docs/runbooks/2026-07-07_2137-rabbitmq-topology-adoption.md) |
| ThrowOnFailure | [`docs/runbooks/2026-07-07_2137-throwonfailure-adoption.md`](docs/runbooks/2026-07-07_2137-throwonfailure-adoption.md) |
| RPC adoption | [`docs/runbooks/2026-07-04_2130-sentandwait-rpc-adoption.md`](docs/runbooks/2026-07-04_2130-sentandwait-rpc-adoption.md) |
| Метрики и алерты | [`docs/runbooks/2026-07-04_0845-metrics-and-alerting.md`](docs/runbooks/2026-07-04_0845-metrics-and-alerting.md) |
| Outbox в транзакции | [`docs/examples/2026-07-03_1639-ef-outbox-transaction.md`](docs/examples/2026-07-03_1639-ef-outbox-transaction.md) |
| Abandoned outbox replay | [`docs/runbooks/2026-07-03_2216-abandoned-outbox-replay.md`](docs/runbooks/2026-07-03_2216-abandoned-outbox-replay.md) |
| REST adoption (SentAndWait + outbox) | [`docs/runbooks/2026-07-10_1330-rest-sentandwait-adoption.md`](docs/runbooks/2026-07-10_1330-rest-sentandwait-adoption.md) |
| REST inbound webhooks | [`docs/runbooks/2026-07-10_1800-rest-webhook-adoption.md`](docs/runbooks/2026-07-10_1800-rest-webhook-adoption.md) |
| REST AsyncOutbox HTTP | [`docs/runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md`](docs/runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md) |
| REST-транспорт (план) | [`docs/plans/2026-07-10_0853-rest-implementation.md`](docs/plans/2026-07-10_0853-rest-implementation.md) |
| REST-транспорт (статус фаз 1–5) | [`docs/2026-07-10_1507-rest-implementation-status.md`](docs/2026-07-10_1507-rest-implementation-status.md) |
| Полный указатель | [`docs/README.md`](docs/README.md) |
| Backlog проекта | [`docs/2026-07-06_1519-remaining-backlog-summary.md`](docs/2026-07-06_1519-remaining-backlog-summary.md) |

## Локализация

Сообщения через `SR.T(...)`. Встроенные resx-ресурсы (`ru`, `en`). Замена провайдера: `LocalizationBootstrap`.

## Лицензия

[MIT](LICENSE)
