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
| `AutomaticRecoveryEnabled` | `true` | Автовосстановление соединения |

Очередь должна существовать на брокере — слушатель подключается к ней через `QueueDeclarePassive`.

**Гарантии доставки (consumer):** ack выполняется **после** завершения обработки (в том числе при `Asynchronously=true`). При ошибке — `BasicNack` с настраиваемым `RequeueOnFailure`. Для poison messages настройте DLQ на брокере (`x-dead-letter-exchange` на основной очереди).

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
План RabbitMQ SentAndWait: [`docs/plans/2026-07-04_0904-rabbitmq-sentandwait.md`](docs/plans/2026-07-04_0904-rabbitmq-sentandwait.md).  
Roadmap P3: [`docs/plans/2026-07-04_0930-post-analysis-roadmap.md`](docs/plans/2026-07-04_0930-post-analysis-roadmap.md).  
План закрытия главных рисков (v1.0): [`docs/plans/2026-07-04_2130-remaining-risks-mitigation.md`](docs/plans/2026-07-04_2130-remaining-risks-mitigation.md).  
План SentAndWait RPC для critical flows (R1/R2): [`docs/plans/2026-07-04_2242-sentandwait-rpc-critical-flows.md`](docs/plans/2026-07-04_2242-sentandwait-rpc-critical-flows.md).  
План реализации (фазы 0–3, PR-ы): [`docs/plans/2026-07-04_2244-sentandwait-rpc-implementation.md`](docs/plans/2026-07-04_2244-sentandwait-rpc-implementation.md).  
Статус реализации (фазы 1–2): [`docs/2026-07-04_2301-sentandwait-rpc-implementation-status.md`](docs/2026-07-04_2301-sentandwait-rpc-implementation-status.md).  
Указатель документации: [`docs/README.md`](docs/README.md).

## Observability

Пакет `IntegrationFlow.Metrics.OpenTelemetry` реализует `IIntegrationFlowMetrics` через `System.Diagnostics.Metrics` (совместимо с OpenTelemetry SDK и Prometheus exporter):

```csharp
using IntegrationFlow.DependencyInjection;
using IntegrationFlow.Metrics.OpenTelemetry.DependencyInjection;

services.AddIntegrationFlow();
services.AddIntegrationFlowOpenTelemetryMetrics();

// В host app — экспорт метрик (пример с OpenTelemetry + Prometheus):
// builder.Services.AddOpenTelemetry()
//     .WithMetrics(m => m.AddMeter("IntegrationFlow").AddPrometheusExporter());
```

Метрики:

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

Runbook алертов: [`docs/runbooks/2026-07-04_0845-metrics-and-alerting.md`](docs/runbooks/2026-07-04_0845-metrics-and-alerting.md).

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

Конфигурация `RabbitMqRequestReply`: `MaxConcurrentRequests` (default `1`, `0` = без лимита), `ReuseConnection` (переиспользование TCP), `ReuseReplyConnection` (default `true` — pool channel на server-side), `SslEnabled` / `SslServerName` (AMQPS).

**Idempotent sync RPC (фаза 1):** задайте `MessageId` через `WithMessageId()` или `TransmitData(data, messageId)`; на server — `IRequestReplyResponseStore` + `RabbitMqRpcServerPipeline`. При timeout включите `SentAndWaitIntegrationOptions.RetryOnTimeout = true` (retry с тем же MessageId).

**Async RPC для critical flows (фаза 2):** `RequestMode: AsyncOutbox` + `ResponseQueueName` в конфиге; stage через `IRpcPendingEnqueue` / `DbContext.EnqueueRpcRequest()` или high-level `CreateSentAndWaitAsyncOutboxIntegration` → `CreatePendingRequest()` в TX; ожидание — `IntegrateWithResultAsync(pendingId)`; relay — `AddIntegrationFlowRpcPendingRelay()`; correlation — `AddIntegrationFlowRabbitMqRpcResponseCorrelation()`; EF — `AddIntegrationFlowEfRpcPending<TContext>()`.

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
