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
└── tests/IntegrationFlow.Core.Tests/  # Модульные тесты (xUnit)
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
Полный анализ решения и рисков: [`docs/2026-07-03_2201-integrationflow-full-analysis.md`](docs/2026-07-03_2201-integrationflow-full-analysis.md).  
Указатель документации: [`docs/README.md`](docs/README.md).

## Локализация

Пользовательские сообщения выводятся через `SR.T(...)`. По умолчанию используются встроенные resx-ресурсы (`ru`, `en`). Провайдер локализации можно заменить через `LocalizationBootstrap`.

## Лицензия

Проект распространяется под лицензией [MIT](LICENSE).
