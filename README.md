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

Создайте класс конфигурации, унаследованный от `RabbitMqConfiguration`:

```csharp
public sealed class MyRabbitMqConfiguration : RabbitMqConfiguration
{
    public MyRabbitMqConfiguration()
    {
        HostName = "localhost";
        Port = 5672;
        UserName = "guest";
        Password = "guest";
        VirtualHost = "/";
        QueueName = "integration.inbox";
        PrefetchCount = 1;
    }
}
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
