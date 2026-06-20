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

Пример — `SampleRabbitMqReceiveAndProcessLauncher` в `00Samples/ReceiveAndProcess/`:

```csharp
public sealed class SampleRabbitMqReceiveAndProcessLauncher : IReceiveAndProcessLauncher
{
    public void Run()
    {
        var publisher = PublisherBase.Create<RabbitMqPublisher, SampleRabbitMqIntegrationPublisherSide>(
            Logger.Create());
        publisher.BeginReceiving();
    }
}
```

### Настройка подключения

Настройки подключения хранятся в файле `rabbitmq.json` рядом с приложением. Пример файла — в `00Samples/ReceiveAndProcess/rabbitmq.json`:

```json
{
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "QueueName": "integration.inbox",
    "PrefetchCount": 1,
    "Asynchronously": true,
    "AutomaticRecoveryEnabled": true,
    "ClientProvidedName": "IntegrationFlow.RabbitMqListener"
  }
}
```

Для организации создайте класс конфигурации, унаследованный от `RabbitMqConfiguration`, и загрузите значения через `RabbitMqConfigurationLoader`:

```csharp
public sealed class MyRabbitMqConfiguration : RabbitMqConfiguration
{
    public MyRabbitMqConfiguration()
    {
        RabbitMqConfigurationLoader.Populate(this);
    }
}
```

Также можно загрузить конфигурацию напрямую:

```csharp
var configuration = RabbitMqConfigurationLoader.Load();
// или из указанного файла:
var configuration = RabbitMqConfigurationLoader.LoadFromFile("C:\\config\\rabbitmq.json");
// после реализации multi-config — профиль по имени:
// var configuration = RabbitMqConfigurationLoader.LoadProfile("Inbox");
```

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
