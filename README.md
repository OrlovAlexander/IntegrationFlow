# IntegrationFlow

Библиотека для построения интеграций между системами. Предоставляет единый каркас для трёх сценариев обмена данными и набор готовых реализаций транспортов.

## Сценарии интеграции

| Сценарий | Описание |
|----------|----------|
| **ReceiveAndProcess** | Получить входящее сообщение и обработать его (consumer). |
| **SentAndWait** | Отправить данные и дождаться ответа (request/response). |
| **SentAndForgot** | Отправить данные без ожидания ответа (fire-and-forget). |

Каждый сценарий расширяется через набор интерфейсов: конфигурация, подключение, форматирование, валидация, логирование и т.д.

## Структура проекта

```
IntegrationFlow/
├── src/IntegrationFlow.Core/          # Основная библиотека (net8.0, netstandard2.0)
│   └── Contexts/Integrations/
│       ├── 00Samples/                 # Примеры использования
│       ├── 00InnerUsage/              # Встроенные реализации транспортов
│       │   └── RabbitMq/                # Реализация для RabbitMQ
│       ├── 01Infrastructure/          # Логирование, локализация
│       ├── 02Application/             # Прикладные интерфейсы
│       └── 03Domain/                    # Доменная модель и базовые классы
├── src/IntegrationFlow.EntityFrameworkCore/  # EF stores (net8.0)
├── src/IntegrationFlow.Metrics.OpenTelemetry/  # Metrics (net8.0)
└── tests/                             # Модульные и integration-тесты (xUnit)
```

## Требования

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) или новее

## Сборка и тесты

```bash
dotnet build IntegrationFlow.sln
dotnet test IntegrationFlow.sln --filter "Category!=Integration"
dotnet test IntegrationFlow.sln --filter "Category=Integration"
```

> Integration-тесты требуют Docker (Testcontainers).

## Подключение через DI

```csharp
using IntegrationFlow.DependencyInjection;

services.AddIntegrationFlow();
```

Метод `AddIntegrationFlow` регистрирует базовые сервисы интеграции и встраивает локализацию из resx-ресурсов.

## RabbitMQ (ReceiveAndProcess)

Реализация прослушивания очереди RabbitMQ находится в `00InnerUsage/RabbitMq/ReceiveAndProcess/`. Слушатель работает асинхронно, потокобезопасно и подтверждает сообщения вручную (`manual ack`).

### Запуск через IHost (рекомендуется, .NET 8+)

```csharp
using IntegrationFlow.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddIntegrationFlow();
        services.AddIntegrationFlowRabbitMqListener("Inbox", message =>
        {
            // обработка входящего сообщения
        });
        // services.AddIntegrationFlowRabbitMqListener("Orders", sp => sp.GetRequiredService<IOrdersInboxHandler>());
    })
    .Build();

await host.RunAsync();
```

### Health checks (.NET 8+)

Для Kubernetes readiness и мониторинга доступны health checks транспортных workers:

```csharp
using IntegrationFlow.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

services.AddIntegrationFlow();
services.AddIntegrationFlowRabbitMqListener("Inbox", message => { /* ... */ });
services.AddIntegrationFlowOutboxRelay();
services.AddIntegrationFlowRabbitMqHealthChecks(options =>
{
    options.MaxReconnectAttemptsBeforeUnhealthy = 5;
    options.OutboxRelayMaxConsecutiveFailures = 5;
});

// ASP.NET Core:
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

| Check | Имя | Unhealthy когда |
|-------|-----|-----------------|
| Listener | `integrationflow.rabbitmq.listener` | Нет активного соединения или reconnect attempts ≥ порога |
| Outbox relay | `integrationflow.rabbitmq.outbox_relay` | Подряд ≥ N неудачных relay batch |
| RPC correlation | `integrationflow.rabbitmq.rpc_correlation` | AsyncOutbox response consumer отключён / reconnect ≥ порога |

Если компонент не зарегистрирован в DI, соответствующий check возвращает **Healthy** (no-op).

Legacy `IReceiveAndProcessLauncher` + `BeginReceiving()` сохранён для обратной совместимости, но помечен `[Obsolete]` на net8.0.

### Пример запуска (legacy launcher)

**Один профиль (Inbox):**

```csharp
public sealed class SampleRabbitMqReceiveAndProcessLauncher : IReceiveAndProcessLauncher
{
    public void Run()
    {
        var publisher = PublisherBase.Create<RabbitMqPublisher, InboxRabbitMqPublisherSide>(Logger.Create());
        publisher.BeginReceiving();
    }
}
```

**Все профили параллельно:**

```csharp
public sealed class SampleRabbitMqAllProfilesReceiveAndProcessLauncher : IReceiveAndProcessLauncher
{
    public void Run()
    {
        foreach (var configuration in RabbitMqConfigurationLoader.LoadAll())
        {
            var side = new NamedRabbitMqIntegrationPublisherSide(configuration.Name);
            var publisher = PublisherBase.Create<RabbitMqPublisher>(Logger.Create(), side);
            publisher.BeginReceiving();
        }
    }
}
```

### Настройка подключения

Настройки хранятся в `rabbitmq.json` рядом с приложением. Поддерживаются **именованные профили** и **legacy flat**-формат.

**Приоритет источников** (от низкого к высокому): `rabbitmq.json` → **environment variables** → **host `IConfiguration`** (appsettings, user secrets).

Environment variables используют разделитель `__` (двойное подчёркивание):

```bash
# Linux / Docker / Kubernetes
export RabbitMq__Inbox__HostName=rabbit.prod.internal
export RabbitMq__Inbox__Password=secret
export RabbitMqPublish__OrdersOut__HostName=rabbit.prod.internal
```

В ASP.NET Core / Worker передайте host configuration:

```csharp
var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    services.AddIntegrationFlow();
    services.AddIntegrationFlowRabbitMq(context.Configuration);
    services.AddIntegrationFlowRabbitMqListener("Inbox", message => { /* ... */ });
});
```

Loaders также поддерживают прямую загрузку из `IConfiguration`:

```csharp
var profile = RabbitMqConfigurationLoader.LoadProfile("Inbox", hostConfiguration);
```

**Именованные профили** (рекомендуется):

```json
{
  "RabbitMq": {
    "Inbox": {
      "HostName": "localhost",
      "Port": 5672,
      "UserName": "guest",
      "Password": "guest",
      "VirtualHost": "/",
      "QueueName": "integration.inbox",
      "PrefetchCount": 1,
      "Asynchronously": true,
      "AutomaticRecoveryEnabled": true,
      "ClientProvidedName": "IntegrationFlow.InboxListener"
    },
    "Orders": {
      "HostName": "localhost",
      "QueueName": "orders.inbox",
      "ClientProvidedName": "IntegrationFlow.OrdersListener"
    }
  }
}
```

**Legacy flat** (один профиль `Default`):

```json
{
  "RabbitMq": {
    "HostName": "localhost",
    "QueueName": "integration.inbox"
  }
}
```

### API загрузки

| Метод | Назначение |
|-------|------------|
| `LoadProfile("Inbox")` | Профиль по имени из default-файла |
| `LoadAll()` | Все профили |
| `Load()` / `LoadSingle(path)` | Единственный профиль (ошибка, если профилей несколько) |
| `LoadFromFile(path)` | Только legacy flat-формат |

```csharp
var inbox = RabbitMqConfigurationLoader.LoadProfile("Inbox");

public sealed class MyRabbitMqConfiguration : RabbitMqConfiguration
{
    public MyRabbitMqConfiguration()
    {
        RabbitMqConfigurationLoader.PopulateProfile(this, "Inbox");
    }
}

internal sealed class InboxRabbitMqPublisherSide : RabbitMqIntegrationPublisherSideBase
{
    protected override string ConfigurationName => "Inbox";
}
```

Для параллельного прослушивания нескольких очередей нужен **отдельный publisher на каждый профиль** (отдельный side-класс или `NamedRabbitMqIntegrationPublisherSide`). `ProcessorBase` кешируется с учётом profile/side через `GetPublisherCacheKey()`.

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| `HostName` | `localhost` | Хост брокера |
| `Port` | `5672` | Порт брокера |
| `UserName` / `Password` | `guest` / `guest` | Учётные данные |
| `VirtualHost` | `/` | Виртуальный хост |
| `QueueName` | — | Имя очереди для прослушивания (обязательно) |
| `PrefetchCount` | `1` | Количество неподтверждённых сообщений |
| `RequeueOnFailure` | `false` | Повторно ставить сообщение в очередь при ошибке обработки |
| `MaxRetryCount` | `0` | Лимит попыток (0 = без ограничения); после лимита nack без requeue (DLQ) |
| `AutomaticRecoveryEnabled` | `true` | Client-level recovery в `ConnectionFactory`; long-running listener также использует reconnect loop каркаса |
| `SslEnabled` / `SslServerName` | `false` / — | AMQPS (обычно порт `5671`) |

Очередь должна существовать на брокере — слушатель подключается к ней через `QueueDeclarePassive`.

**Гарантии доставки (consumer):** ack выполняется **после** завершения обработки (в том числе при `Asynchronously=true`). При ошибке — `BasicNack` с настраиваемым `RequeueOnFailure`. Для poison messages настройте DLQ на брокере (`x-dead-letter-exchange` на основной очереди).

**Reconnect при разрыве соединения:** `RabbitMqListenerWorker` автоматически переподключается при `ConnectionShutdown` или ошибке сессии (exponential backoff 1–30 с). Завершение приложения — только по `CancellationToken` (`host.StopAsync()`). Флаг `AutomaticRecoveryEnabled` в `ConnectionFactory` дополняет client-level recovery; long-running consumer опирается на reconnect loop каркаса.

**Graceful shutdown:** при остановке host listener отменяет consumer (`BasicCancel`), ждёт завершения in-flight обработок (до 30 с), затем закрывает channel. Сообщения, полученные во время shutdown, возвращаются в очередь через **nack requeue** (безопасно с dedup). Метрика: `integrationflow.message.shutdown_requeue`.

**Идемпотентность:** реализуйте `IMessageDeduplicationStore` и верните его из `IntegrationProcessorSideBase.GetMessageDeduplicationStore`. Sample: `InMemoryMessageDeduplicationStore`. При сбое обработки lock снимается через `ReleaseProcessingAsync`; параллельная доставка того же `MessageId` получает nack requeue.

## RabbitMQ (SentAndForgot)

Публикация в очередь/exchange с **publisher confirms** (по умолчанию включены), `MessageId` и `IntegrateWithResult()`:

```csharp
var integration = orgIntegration.CreateSentAndForgotIntegration<SampleRabbitMqSentAndForgotProvider>(
    oppositeSideCode: "OrdersOut",
    srcData: orderEvent);

var result = integration.IntegrateWithResult();
if (!result.Success)
{
    // обработать ошибку publish/reconnect
}

// Опционально: Integrate() бросает IntegrationFailedException при ThrowOnFailure = true
SentAndForgotIntegrationOptions.ThrowOnFailure = true;
integration.Integrate();
```

| Параметр (`RabbitMqPublish`) | По умолчанию | Описание |
|------------------------------|--------------|----------|
| `PublisherConfirmsEnabled` | `true` | Ждать confirm от брокера после `BasicPublish` |
| `ConfirmTimeoutSeconds` | `30` | Таймаут ожидания confirm |
| `Persistent` | `true` | `DeliveryMode=2` |
| `Mandatory` | `false` | При `true` — ошибка, если сообщение не маршрутизируется |
| `ReuseConnection` | `false` | Переиспользовать TCP для high-frequency direct publish |
| `SslEnabled` / `SslServerName` | `false` / — | AMQPS (обычно порт `5671`) |

### Transactional Outbox

Для атомарности «БД + сообщение» используйте outbox и relay worker. **Enqueue в prod** — через `IOutboxEnqueue` или `DbContext.EnqueueOutboxMessage()` в той же TX, что и бизнес-данные (см. [пример](docs/examples/2026-07-03_1639-ef-outbox-transaction.md)).

```csharp
services.AddIntegrationFlow();
services.AddSingleton<IOutboxStore, InMemoryOutboxStore>(); // relay worker
services.AddIntegrationFlowOutboxRelay(options =>
{
    options.BatchSize = 20;
    options.PollingInterval = TimeSpan.FromSeconds(5);
    options.LockDuration = TimeSpan.FromSeconds(60);
    options.MaxAttempts = 10;
    options.RetryBackoffBase = TimeSpan.FromSeconds(5);
});
```

> **Direct publish после commit БД** — антипаттерн. Для атомарности используйте outbox в той же транзакции, что и бизнес-данные.

**EF Core store (production):** пакет `IntegrationFlow.EntityFrameworkCore`:

```csharp
services.AddDbContext<MyDbContext>(...);           // scoped — для IOutboxEnqueue
services.AddDbContextFactory<MyDbContext>(...);    // factory — для relay IOutboxStore
services.AddIntegrationFlowEfOutbox<MyDbContext>();
services.AddIntegrationFlowEfDeduplication<MyDbContext>(options =>
{
    options.ProcessedRetention = TimeSpan.FromDays(30);
    options.ProcessingLockDuration = TimeSpan.FromMinutes(15);
});
```

В `OnModelCreating`: `modelBuilder.ConfigureIntegrationFlow();`

**Integration tests** (требуют Docker):

```bash
dotnet test --filter "Category=Integration"
```

Подробнее: [`docs/plans/2026-07-03_1639-production-readiness.md`](docs/plans/2026-07-03_1639-production-readiness.md).  
Полный анализ решения и рисков: [`docs/2026-07-04_2234-integrationflow-full-analysis.md`](docs/2026-07-04_2234-integrationflow-full-analysis.md).  
Отчёт по видам интеграций, сценариям и gap-листу: [`docs/2026-07-04_2338-integration-types-full-report.md`](docs/2026-07-04_2338-integration-types-full-report.md).  
Полный анализ RabbitMQ (gaps и roadmap): [`docs/2026-07-04_2352-rabbitmq-full-analysis.md`](docs/2026-07-04_2352-rabbitmq-full-analysis.md).  
План закрытия RabbitMQ transport gaps G1–G5 (P1): [`docs/plans/2026-07-06_1445-rabbitmq-g1-g5-mitigation.md`](docs/plans/2026-07-06_1445-rabbitmq-g1-g5-mitigation.md).  
Статус реализации G1–G5: [`docs/2026-07-06_1456-rabbitmq-g1-g5-implementation-status.md`](docs/2026-07-06_1456-rabbitmq-g1-g5-implementation-status.md).  
Актуальный backlog: [`docs/2026-07-06_1519-remaining-backlog-summary.md`](docs/2026-07-06_1519-remaining-backlog-summary.md).  
План RabbitMQ P2 (resilience): [`docs/plans/2026-07-06_1519-rabbitmq-p2-resilience-hardening.md`](docs/plans/2026-07-06_1519-rabbitmq-p2-resilience-hardening.md).  
Статус реализации P2 core: [`docs/2026-07-06_1617-rabbitmq-p2-implementation-status.md`](docs/2026-07-06_1617-rabbitmq-p2-implementation-status.md).  
План RabbitMQ SentAndWait: [`docs/plans/2026-07-04_0904-rabbitmq-sentandwait.md`](docs/plans/2026-07-04_0904-rabbitmq-sentandwait.md).  
Roadmap P3: [`docs/plans/2026-07-04_0930-post-analysis-roadmap.md`](docs/plans/2026-07-04_0930-post-analysis-roadmap.md).  
План закрытия главных рисков (v1.0): [`docs/plans/2026-07-04_2130-remaining-risks-mitigation.md`](docs/plans/2026-07-04_2130-remaining-risks-mitigation.md).  
План SentAndWait RPC для critical flows (R1/R2): [`docs/plans/2026-07-04_2242-sentandwait-rpc-critical-flows.md`](docs/plans/2026-07-04_2242-sentandwait-rpc-critical-flows.md).  
План реализации (фазы 0–3, PR-ы): [`docs/plans/2026-07-04_2244-sentandwait-rpc-implementation.md`](docs/plans/2026-07-04_2244-sentandwait-rpc-implementation.md).  
Статус реализации (фазы 1–2): [`docs/2026-07-04_2301-sentandwait-rpc-implementation-status.md`](docs/2026-07-04_2301-sentandwait-rpc-implementation-status.md).  
Статус реализации P3 ops: [`docs/2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md`](docs/2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md).  
Указатель документации: [`docs/README.md`](docs/README.md).

## Observability

Полный гайд P3 (health, metrics, logging, tracing, config): [`docs/2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md`](docs/2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md).  
Runbook алертов и порогов: [`docs/runbooks/2026-07-04_0845-metrics-and-alerting.md`](docs/runbooks/2026-07-04_0845-metrics-and-alerting.md).

### Production quick-start (.NET 8+)

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
    .WithMetrics(metrics => metrics
        .AddMeter("IntegrationFlow")
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
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

**appsettings.Production.json** (overlay поверх `rabbitmq.json`):

```json
{
  "RabbitMq": {
    "Inbox": {
      "HostName": "rabbit.prod.internal",
      "PrefetchCount": 10
    }
  },
  "IntegrationFlow": {
    "HealthChecks": {
      "MaxReconnectAttemptsBeforeUnhealthy": 5,
      "OutboxRelayMaxConsecutiveFailures": 5
    }
  }
}
```

Секреты — через environment variables (`RabbitMq__Inbox__Password`, см. раздел RabbitMQ выше).

Метрики (`IntegrationFlow.Metrics.OpenTelemetry` + `System.Diagnostics.Metrics`):

| Имя | Тип | Описание |
|-----|-----|----------|
| `integrationflow.message.processed` | Counter | Обработанные inbox-сообщения (`profile`, `success`) |
| `integrationflow.message.processing.duration` | Histogram | Длительность обработки (секунды) |
| `integrationflow.outbox.relay.published` | Counter | Успешный relay outbox |
| `integrationflow.outbox.relay.failed` | Counter | Ошибки relay |
| `integrationflow.outbox.relay.abandoned` | Counter | Abandoned после max attempts |
| `integrationflow.outbox.pending` | Gauge | Текущий backlog pending |
| `integrationflow.requestreply.completed` | Counter | Завершённые RPC-запросы (`profile`, `success`, `timeout`) |
| `integrationflow.requestreply.retry_after_timeout` | Counter | Retry RPC после timeout (`profile`) |
| `integrationflow.requestreply.duration` | Histogram | Длительность RPC round-trip (секунды) |
| `integrationflow.rpc.pending.relay.published` | Counter | AsyncOutbox: успешно relayed pending requests |
| `integrationflow.rpc.pending.relay.failed` | Counter | AsyncOutbox: ошибки relay |
| `integrationflow.rpc.pending.relay.abandoned` | Counter | AsyncOutbox: abandoned после max attempts |
| `integrationflow.rpc.pending.awaiting` | Gauge | AsyncOutbox: awaiting response |
| `integrationflow.rpc.pending.completed` | Counter | AsyncOutbox: завершённые pending (`profile`, `success`, `timeout`) |
| `integrationflow.rpc.pending.duration` | Histogram | AsyncOutbox: round-trip от staging (секунды) |
| `integrationflow.listener.reconnect` | Counter | Переподключения listener после разрыва (`profile`) |
| `integrationflow.message.shutdown_requeue` | Counter | Nack requeue при graceful shutdown (`profile`) |
| `integrationflow.message.consumer.outcome` | Counter | Consumer ack outcomes (`profile`, `reason`) |
| `integrationflow.connection.pool.size` | Gauge | Размер pool TCP-подключений (`kind=rpc|publish`) |
| `integrationflow.broker.connected` | Gauge | Подключение к брокеру (`profile`, `kind=listener|outbox_relay|rpc_correlation`; 1=connected, 0=disconnected) |

### P3 checklist (production observability)

| ID | Возможность | Регистрация / действие |
|----|-------------|------------------------|
| P3-1 | Health checks | `AddIntegrationFlowRabbitMqHealthChecks()` + `MapHealthChecks("/health/ready")` |
| P3-2 | Config overlay | `AddIntegrationFlowRabbitMq(IConfiguration)` + env vars `RabbitMq__*` |
| P3-3 | Broker gauge | `AddIntegrationFlowOpenTelemetryMetrics()` |
| P3-4 | Distributed tracing | `AddSource(IntegrationFlowRabbitMqActivitySource.Name)` в OTel tracing |
| P3-5 | Structured logging | `AddIntegrationFlow()` + host `ILogger` (Serilog / OTel logs) |
| P3-6 | Consumer outcomes | Автоматически с метриками; алерты — в runbook |
| P3-7 | Документация | Этот раздел + runbook + [`P3 status`](docs/2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md) |

Runbook алертов: [`docs/runbooks/2026-07-04_0845-metrics-and-alerting.md`](docs/runbooks/2026-07-04_0845-metrics-and-alerting.md).

### Structured logging (P3-5)

Transport workers attach structured fields via `ILogger.BeginScope` (совместимо с Serilog, OpenTelemetry log exporter, JSON console):

| Поле | Описание |
|------|----------|
| `integrationflow.profile` | Имя профиля RabbitMQ |
| `integrationflow.message_id` | AMQP `MessageId` |
| `integrationflow.correlation_id` | AMQP `CorrelationId` |
| `integrationflow.delivery_tag` | Delivery tag consumer |
| `integrationflow.kind` | `listener`, `outbox_relay`, `rpc_correlation`, `publish`, `request_reply` |
| `integrationflow.outcome` | `consume_started`, `ack`, `nack`, `requeue`, `dedup_skip`, `in_progress_requeue`, `shutdown_requeue`, `published`, `publish_failed`, `abandoned` |

Пример вывода (Serilog compact JSON):

```json
{"@t":"...","@mt":"RabbitMQ listener. Сообщение подтверждено.","integrationflow.profile":"Inbox","integrationflow.message_id":"abc","integrationflow.correlation_id":"xyz","integrationflow.delivery_tag":42,"integrationflow.kind":"listener","integrationflow.outcome":"ack"}
```

Поля добавляются автоматически при `AddIntegrationFlow()` + стандартном `ILogger` host. Без `ILoggerFactory` используется `NullIntegrationLogger` (scope no-op).

### Consumer outcome metrics (P3-6)

Counter `integrationflow.message.consumer.outcome` с тегами `profile`, `reason`:

| `reason` | Когда |
|----------|-------|
| `nack` | `BasicNack` без requeue (ошибка обработки, `RequeueOnFailure=false` / DLQ) |
| `requeue` | `BasicNack` с requeue после ошибки обработки |
| `dedup_skip` | Сообщение уже обработано (`IMessageDeduplicationStore`) |
| `in_progress_requeue` | Параллельная доставка того же `MessageId` (`MessageProcessingInProgressException`) |

`shutdown_requeue` по-прежнему учитывается отдельным counter `integrationflow.message.shutdown_requeue`.

### Distributed tracing (P3-4)

RabbitMQ transport propagates W3C `traceparent` / `tracestate` через AMQP headers и создаёт `Activity` spans:

| Operation | Span | Kind |
|-----------|------|------|
| SentAndForgot / outbox publish | `rabbitmq.publish` | Producer |
| SentAndWait RPC request | `rabbitmq.request` | Producer |
| RPC reply (server) | `rabbitmq.reply` | Producer |
| Inbox consume | `rabbitmq.receive` | Consumer |
| Sync RPC response (client) | `rabbitmq.response` | Consumer |
| AsyncOutbox response correlation | `rabbitmq.response` | Consumer |

Регистрация в host app (OpenTelemetry .NET SDK):

```csharp
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(IntegrationFlowRabbitMqActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

Пример сквозного trace: HTTP request → `rabbitmq.publish` → `rabbitmq.receive` (другой сервис) → handler → `rabbitmq.reply` → `rabbitmq.response`.

> Parent context extraction (`ActivityContext.TryParse`) доступен на **net8.0**. На netstandard2.0 spans создаются, inject работает при наличии `Activity.Current`, extract parent — best-effort.

## NuGet

```bash
dotnet pack IntegrationFlow.sln -c Release
```

Пакеты: `IntegrationFlow.Core`, `IntegrationFlow.EntityFrameworkCore`, `IntegrationFlow.Metrics.OpenTelemetry`.  
Release process: [`docs/runbooks/2026-07-04_0845-nuget-release.md`](docs/runbooks/2026-07-04_0845-nuget-release.md).

## RabbitMQ (SentAndWait)

Синхронный request-reply через RabbitMQ (`ReplyTo` + `CorrelationId`, по умолчанию `DirectReplyTo`):

```csharp
using IntegrationFlow.DependencyInjection;

services.AddIntegrationFlow();

var integration = orgIntegration.CreateSentAndWaitIntegration<SampleRabbitMqSentAndWaitProvider>(
    oppositeSideCode: "OrdersRpc",
    srcData: new { OrderId = 42 });

var handler = orgIntegration.GetSentAndWaitResultHandler<SampleRabbitMqSentAndWaitProvider>("OrdersRpc");
integration.Integrate(handler);
```

Секция конфигурации в `rabbitmq.json` — `RabbitMqRequestReply`. На стороне сервера используйте `RabbitMqReplyPublisher` для ответа на `RabbitMqReceivedMessage.ReplyTo`.

План реализации: [`docs/plans/2026-07-04_0904-rabbitmq-sentandwait.md`](docs/plans/2026-07-04_0904-rabbitmq-sentandwait.md).

### Async API

```csharp
var integration = orgIntegration.CreateSentAndWaitIntegration<SampleRabbitMqSentAndWaitProvider>(
    oppositeSideCode: "OrdersRpc",
    srcData: new { OrderId = 42 });

var handler = orgIntegration.GetSentAndWaitResultHandler<SampleRabbitMqSentAndWaitProvider>("OrdersRpc");

// Рекомендуется в ASP.NET Core / IHost:
await integration.IntegrateAsync(handler, cancellationToken);

// Или typed result:
var result = await integration.IntegrateWithResultAsync(cancellationToken);
if (!result.Success)
{
    if (result.TimedOut) { /* retry с новым CorrelationId */ }
}
```

Конфигурация `RabbitMqRequestReply`: `MaxConcurrentRequests` (default `1`, `0` = без лимита), `ReuseConnection` (переиспользование TCP), `ReuseReplyConnection` (default `true` — pool channel на server-side), `ManualReplyAck` (default `false` — при `true` reply ack'ится вручную после корреляции; рекомендуется для unstable process), `PublisherConfirmsEnabled` / `ConfirmTimeoutSeconds` (confirm request publish), `ReplyMandatory` (opt-in mandatory на server-side reply), `SslEnabled` / `SslServerName` (AMQPS).

**Idempotent sync RPC (фаза 1):** задайте `MessageId` через `WithMessageId()` или `TransmitData(data, messageId)`; на server — `IRequestReplyResponseStore` + `RabbitMqRpcServerPipeline`. При timeout включите `SentAndWaitIntegrationOptions.RetryOnTimeout = true` (retry с тем же MessageId).

**Async RPC для critical flows (фаза 2–3):** `RequestMode: AsyncOutbox` + `ResponseQueueName` в конфиге; stage через `CreateSentAndWaitAsyncOutboxIntegration` → `CreatePendingRequest()` в TX; ожидание — `IntegrateWithResultAsync(pendingId)`; relay — `AddIntegrationFlowRpcPendingRelay()`; correlation — `AddIntegrationFlowRabbitMqRpcResponseCorrelation()`; compensation — `AddIntegrationFlowEfOutboxRpcCompensation<T>()` + `AddIntegrationFlowRpcPendingCompensation()`; cleanup — `AddIntegrationFlowMaintenance()`; EF — `AddIntegrationFlowEfRpcPending<TContext>()`.

### Server-side RPC handler

Reply публикуется **до return** из handler (иначе ack без ответа):

```csharp
using IntegrationFlow.DependencyInjection;

services.AddIntegrationFlow();
services.AddIntegrationFlowRabbitMqListener("OrdersRpc",
    IntegrationFlow.Contexts.Integrations._00Samples.SentAndWait
        .SampleRabbitMqSentAndWaitRpcServer.CreateHandler("OrdersRpc"));
```

### Production defaults

```csharp
SentAndWaitIntegrationOptions.ThrowOnFailure = true;
SentAndForgotIntegrationOptions.ThrowOnFailure = true;
```

Runbooks: [`docs/runbooks/2026-07-04_2130-sentandwait-rpc-adoption.md`](docs/runbooks/2026-07-04_2130-sentandwait-rpc-adoption.md), [`docs/runbooks/2026-07-04_2130-production-adoption.md`](docs/runbooks/2026-07-04_2130-production-adoption.md).

### AMQPS (TLS)

```json
{
  "RabbitMq": {
    "Inbox": {
      "HostName": "rabbit.example.com",
      "Port": 5671,
      "SslEnabled": true,
      "SslServerName": "rabbit.example.com",
      "QueueName": "integration.inbox"
    }
  },
  "RabbitMqPublish": {
    "OrdersOut": {
      "HostName": "rabbit.example.com",
      "Port": 5671,
      "SslEnabled": true,
      "SslServerName": "rabbit.example.com",
      "PublishTarget": "Queue",
      "QueueName": "orders.outbox"
    }
  },
  "RabbitMqRequestReply": {
    "OrdersRpc": {
      "HostName": "rabbit.example.com",
      "Port": 5671,
      "SslEnabled": true,
      "SslServerName": "rabbit.example.com",
      "QueueName": "orders.rpc"
    }
  }
}
```

### Secrets через environment variables

Не коммитьте prod-пароли в `rabbitmq.json`. В host app переопределяйте через `IConfiguration`:

```bash
RabbitMq__Inbox__Password=secret
RabbitMqRequestReply__OrdersRpc__Password=secret
```

## Локализация

Пользовательские сообщения выводятся через `SR.T(...)`. По умолчанию используются встроенные resx-ресурсы (`ru`, `en`). Провайдер локализации можно заменить через `LocalizationBootstrap`.

## Лицензия

Проект распространяется под лицензией [MIT](LICENSE).
