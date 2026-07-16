# RmqSAF-RmqRAP-Rest — нагрузочные тесты

Нагрузочное тестирование E2E-цепочки **IntegrationFlow**:

**Sender (SAF)** → RabbitMQ → **Bridge (RAP)** → REST → **Storage**

Решение построено на [E2E sample](../Samples/E2E/RmqSAF-RmqRAP-Rest/) и использует [NBomber](https://nbomber.com/) для генерации нагрузки.

## Состав

| Проект | Назначение |
|--------|------------|
| `RmqSAF-RmqRAP-Rest.LoadTests` | Console runner с NBomber-сценариями |

## Сценарии

| Сценарий | Что измеряет |
|----------|--------------|
| `publish_only` | Пропускная способность `POST /api/messages` (Sender → RabbitMQ) |
| `end_to_end` | Полный цикл: publish → ожидание записи в Storage (меньшая rate) |

При `Scenario: Both` (по умолчанию) оба сценария выполняются последовательно. После `publish_only` выполняется **delivery verification** — проверка, что опубликованные `correlationId` появились в Storage.

## Требования

- .NET 10 SDK
- Запущенный E2E-стек (Docker Compose или Aspire)
- Для Compose: storage должен быть доступен с хоста (порт **8081**)

## Подготовка E2E-стека

### Docker Compose (рекомендуется для load tests)

```powershell
cd Samples/E2E/RmqSAF-RmqRAP-Rest
docker compose -f docker-compose.yml `
  -f ../../../e2eTests/docker-compose.loadtests.yml `
  up --build -d
```

Дождитесь `healthy` у всех сервисов:

```powershell
docker compose ps
```

| Сервис | URL |
|--------|-----|
| Sender | http://localhost:8080 |
| Storage | http://localhost:8081 |

### Aspire

```powershell
dotnet run --project Samples/E2E/RmqSAF-RmqRAP-Rest/RmqSAF-RmqRAP-Rest.AppHost
```

Укажите URL из Aspire Dashboard через переменные окружения или CLI:

```powershell
dotnet run --project RmqSAF-RmqRAP-Rest.LoadTests -- `
  --sender http://localhost:5xxx `
  --storage http://localhost:5yyy
```

## Запуск load tests

```powershell
cd e2eTests
dotnet run --project RmqSAF-RmqRAP-Rest.LoadTests
```

### Быстрый smoke-прогон

```powershell
dotnet run --project RmqSAF-RmqRAP-Rest.LoadTests -- `
  --scenario PublishOnly `
  --rate 5 `
  --duration 15
```

### Только end-to-end latency

```powershell
dotnet run --project RmqSAF-RmqRAP-Rest.LoadTests -- `
  --scenario EndToEnd `
  --rate 4 `
  --duration 30
```

## Конфигурация

Настройки в `RmqSAF-RmqRAP-Rest.LoadTests/appsettings.json`, секция `LoadTest`.

Переопределение через переменные окружения с префиксом `LOADTEST_`:

```powershell
$env:LOADTEST_LoadTest__InjectRate = "20"
$env:LOADTEST_LoadTest__DurationSeconds = "120"
dotnet run --project RmqSAF-RmqRAP-Rest.LoadTests
```

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| `InjectRate` | 10 | Сообщений в секунду (publish_only) |
| `DurationSeconds` | 60 | Длительность нагрузки |
| `WarmupSeconds` | 5 | Прогрев NBomber |
| `MinDeliverySuccessRate` | 0.95 | Минимальный % доставки после publish_only |
| `DeliveryVerificationDelaySeconds` | 15 | Пауза перед проверкой storage |

## Результаты и exit codes

NBomber выводит таблицу latency/RPS в консоль и сохраняет HTML/CSV отчёты в `./reports`.

| Exit code | Причина |
|-----------|---------|
| 0 | Успех |
| 1 | Ошибки в NBomber-сценариях |
| 2 | Delivery verification ниже порога |

## Сборка

```powershell
dotnet build RmqSAF-RmqRAP-Rest.LoadTests.sln
```
