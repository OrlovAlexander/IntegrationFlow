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
dotnet test tests/IntegrationFlow.Core.Tests/IntegrationFlow.Core.Tests.csproj
```

## Подключение через DI

```csharp
using IntegrationFlow.DependencyInjection;

services.AddIntegrationFlow();
```

Метод `AddIntegrationFlow` регистрирует базовые сервисы интеграции и встраивает локализацию из resx-ресурсов.

## RabbitMQ (ReceiveAndProcess)

Реализация прослушивания очереди RabbitMQ находится в `00InnerUsage/RabbitMq/ReceiveAndProcess/`. Слушатель работает асинхронно, потокобезопасно и подтверждает сообщения вручную (`manual ack`).

### Пример запуска

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

Для параллельного прослушивания нескольких очередей нужен **отдельный publisher на каждый профиль** (отдельный side-класс или `NamedRabbitMqIntegrationPublisherSide`).

> **Ограничение v1:** `ProcessorBase` кешируется по типу. При `NoOpInboxMessageProcessing` это безопасно; при реальной бизнес-обработке per queue потребуется доработка кеширования processor-а.

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| `HostName` | `localhost` | Хост брокера |
| `Port` | `5672` | Порт брокера |
| `UserName` / `Password` | `guest` / `guest` | Учётные данные |
| `VirtualHost` | `/` | Виртуальный хост |
| `QueueName` | — | Имя очереди для прослушивания (обязательно) |
| `PrefetchCount` | `1` | Количество неподтверждённых сообщений |
| `AutomaticRecoveryEnabled` | `true` | Автовосстановление соединения |

Очередь должна существовать на брокере — слушатель подключается к ней через `QueueDeclarePassive`.

## Локализация

Пользовательские сообщения выводятся через `SR.T(...)`. По умолчанию используются встроенные resx-ресурсы (`ru`, `en`). Провайдер локализации можно заменить через `LocalizationBootstrap`.

## Лицензия

Проект распространяется под лицензией [MIT](LICENSE).
