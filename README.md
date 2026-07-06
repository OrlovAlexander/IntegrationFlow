# IntegrationFlow

Библиотека для построения интеграций между системами на .NET. Единый каркас для трёх сценариев обмена данными и готовая реализация транспорта **RabbitMQ**.

## Типы интеграций

| Сценарий | Когда использовать | Направление | Конфигурация RabbitMQ |
|----------|-------------------|-------------|------------------------|
| **ReceiveAndProcess** | Обработка входящих сообщений из очереди | Consumer | `RabbitMq` |
| **SentAndForgot** | Отправка события без ожидания ответа | Producer | `RabbitMqPublish` |
| **SentAndWait** | Запрос–ответ (RPC) с ожиданием результата | Producer + consumer ответа | `RabbitMqRequestReply` |

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
| Hosted listener | `AddIntegrationFlowRabbitMqListener` (.NET 8+, рекомендуется) |
| TLS | AMQPS через `SslEnabled` / `SslServerName` |

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
| Server handler | `RabbitMqReplyPublisher` — ответ **до return** из handler |
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

### Запуск (.NET 8+, рекомендуется)

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
| `SslEnabled` / `SslServerName` | `false` / — | AMQPS |

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
| `Mandatory` | `false` | Ошибка, если сообщение не маршрутизируется |
| `ReuseConnection` | `false` | Переиспользование TCP |
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

### Синхронный RPC (клиент)

```csharp
services.AddIntegrationFlow();

var integration = orgIntegration.CreateSentAndWaitIntegration<SampleRabbitMqSentAndWaitProvider>(
    oppositeSideCode: "OrdersRpc",
    srcData: new { OrderId = 42 });

var handler = orgIntegration.GetSentAndWaitResultHandler<SampleRabbitMqSentAndWaitProvider>("OrdersRpc");
await integration.IntegrateAsync(handler, cancellationToken);
```

### Server-side handler

Reply публикуется **до return** из handler:

```csharp
services.AddIntegrationFlow();
services.AddIntegrationFlowRabbitMqListener("OrdersRpc",
    SampleRabbitMqSentAndWaitRpcServer.CreateHandler("OrdersRpc"));
```

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
| `SslEnabled` / `SslServerName` | `false` / — | AMQPS |

### Production defaults

```csharp
SentAndWaitIntegrationOptions.ThrowOnFailure = true;
SentAndForgotIntegrationOptions.ThrowOnFailure = true;
```

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
| RPC adoption | [`docs/runbooks/2026-07-04_2130-sentandwait-rpc-adoption.md`](docs/runbooks/2026-07-04_2130-sentandwait-rpc-adoption.md) |
| Метрики и алерты | [`docs/runbooks/2026-07-04_0845-metrics-and-alerting.md`](docs/runbooks/2026-07-04_0845-metrics-and-alerting.md) |
| Outbox в транзакции | [`docs/examples/2026-07-03_1639-ef-outbox-transaction.md`](docs/examples/2026-07-03_1639-ef-outbox-transaction.md) |
| Abandoned outbox replay | [`docs/runbooks/2026-07-03_2216-abandoned-outbox-replay.md`](docs/runbooks/2026-07-03_2216-abandoned-outbox-replay.md) |
| Полный указатель | [`docs/README.md`](docs/README.md) |

## Локализация

Сообщения через `SR.T(...)`. Встроенные resx-ресурсы (`ru`, `en`). Замена провайдера: `LocalizationBootstrap`.

## Лицензия

[MIT](LICENSE)
