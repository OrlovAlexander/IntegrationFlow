# План: замена `EleWise.ELMA.SR.T` на локализацию в IntegrationFlow

**Статус:** выполнено (этапы 1–3, T.1–T.2)  
**Дата:** 2026-06-19  
**Цель:** убрать зависимость Core от ELMA SDK, сохранив совместимый API локализации и возможность подключения ELMA-переводов на стороне хоста.

---

## Контекст

Сейчас `EleWise.ELMA.SR.T` используется только в sample-коде:

- `src/IntegrationFlow.Core/Contexts/Integrations/00Samples/Transmitters/RESTSimpleTransmitter.cs` (2 вызова)

В `IntegrationFlow.Core.csproj` нет ссылки на ELMA SDK — код не собирается автonomously без внешних зависимостей.

Остальной код использует hardcoded-строки и `string.Format` без единого механизма i18n.

---

## Принятое решение

В Core вводится **абстракция локализации** с API, совместимым с ELMA:

```
ILocalizationProvider  →  SR.T(format, args)  →  fallback: string.Format(CurrentCulture, ...)
```

ELMA-адаптер подключается **в host-проекте / плагине**, не в Core.

---

## Этапы выполнения

### Этап 1. Инфраструктура локализации в Core

**Задачи:**

1. Создать интерфейс `ILocalizationProvider`:
   - Путь: `src/IntegrationFlow.Core/Contexts/Integrations/01Infrastructure/Localization/ILocalizationProvider.cs`
   - Метод: `string T(string format, params object[] args)`

2. Создать статический фасад `SR`:
   - Путь: `src/IntegrationFlow.Core/Contexts/Integrations/01Infrastructure/Localization/SR.cs`
   - `Configure(ILocalizationProvider provider)` — регистрация провайдера
   - `T(string format, params object[] args)` — делегирование провайдеру или fallback

3. Fallback-поведение (если провайдер не настроен):
   - `string.Format(CultureInfo.CurrentCulture, format, args)`
   - Русский текст в `format` остаётся рабочим default (как в ELMA)

**Критерий готовности:** Core компилируется без ELMA, API `SR.T(...)` доступен из любого слоя.

---

### Этап 2. Замена вызовов в sample-коде

**Задачи:**

1. В `RESTSimpleTransmitter.cs`:
   - Заменить `EleWise.ELMA.SR.T(...)` → `SR.T(...)`
   - Добавить `using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization`

2. Проверить, что в репозитории не осталось ссылок на `EleWise.ELMA.SR`

**Критерий готовности:** `grep "EleWise.ELMA.SR"` — 0 совпадений.

---

### Этап 3. ELMA-адаптер (отдельный проект или host-слой)

**Задачи:**

1. Создать адаптер (если есть ELMA host-проект):
   - Класс: `ElmaLocalizationProvider : ILocalizationProvider`
   - Реализация: `EleWise.ELMA.SR.T(format, args)`

2. При старте плагина / host:
   ```csharp
   SR.Configure(new ElmaLocalizationProvider());
   ```

**Примечание:** если ELMA host-проекта в этом репозитории нет — зафиксировать адаптер в README плана как пример интеграции для потребителей библиотеки.

**Критерий готовности:** при запуске в ELMA переводы берутся из ELMA Translation; вне ELMA — fallback на исходный текст.

---

### Этап 4. (Опционально) Standalone-локализация через `.resx`

**Задачи:**

1. Добавить `IntegrationFlowResources.resx` (+ `IntegrationFlowResources.en.resx` и др.)
2. Реализовать `ResourceLocalizationProvider`:
   - ключ = исходная строка (как в ELMA Orchard Localizer)
   - fallback на ключ, если перевод не найден
3. Зарегистрировать в standalone host:
   ```csharp
   SR.Configure(new ResourceLocalizationProvider());
   ```

**Критерий готовности:** Core работает с переводами без ELMA SDK.

**Приоритет:** низкий, если основной потребитель — ELMA.

---

### Этап 5. (Постепенно) Миграция hardcoded-строк

Не блокирует этапы 1–3. Выполнять по мере появления переводов.

**Кандидаты на миграцию:**

| Файл | Примеры строк |
|------|---------------|
| `ListenerBase.cs` | «Поток слушателя запущен», «Ошибка запуска» |
| `ProcessorBase.cs` | «Отсутствует обработка результата» |
| `PublisherBase.cs` | «ConfigurationChanged», «BeginReceiving» |
| `SentAndWaitIntegration.cs` | `string.Format("SendAndWait - '{0}'", ...)` |
| `SentAndForgotIntegration.cs` | `SendAndForgot - '{oppositeSide...}'` |

**Правило:** новые пользовательские сообщения — только через `SR.T(...)`.

---

## Структура файлов (итог)

```
src/IntegrationFlow.Core/
  Contexts/Integrations/01Infrastructure/Localization/
    ILocalizationProvider.cs      ← новый
    SR.cs                         ← новый
    ResourceLocalizationProvider.cs  ← опционально, этап 4

  Contexts/Integrations/00Samples/Transmitters/
    RESTSimpleTransmitter.cs      ← правка: SR.T вместо EleWise.ELMA.SR.T

docs/plans/
  localization-sr-replacement.md  ← этот документ
```

---

## Тестирование

### Unit-тесты (рекомендуется добавить)

1. `SR.T` без провайдера → возвращает `string.Format` с `CurrentCulture`
2. `SR.T` с mock-провайдером → вызывает провайдер с теми же аргументами
3. `SR.Configure` → последующие вызовы идут через новый провайдер

### Ручная проверка

- [x] Сборка `IntegrationFlow.Core` (net8.0 и netstandard2.0)
- [x] Нет ссылок на `EleWise.ELMA` в Core
- [x] Sample `RESTSimpleTransmitter` компилируется
- [ ] (При наличии ELMA host) переводы подхватываются после `SR.Configure`

---

## Риски и ограничения

| Риск | Митигация |
|------|-----------|
| Статический `SR.Configure` — глобальное состояние | Допустимо для совместимости с ELMA API; в тестах сбрасывать провайдер |
| Ключ = полная русская строка | Соответствует ELMA Orchard Localizer; не менять формат ключей при миграции |
| netstandard2.0 vs net8.0 | Не добавлять зависимости, специфичные только для net8.0, в Core |

---

## Чеклист выполнения

- [x] **1.1** Создать `ILocalizationProvider`
- [x] **1.2** Создать `SR` с fallback
- [x] **2.1** Заменить вызовы в `RESTSimpleTransmitter.cs`
- [x] **2.2** Убедиться: 0 ссылок на `EleWise.ELMA.SR` в Core
- [x] **3.1** Пример `ElmaLocalizationProvider` → `docs/examples/ElmaLocalizationProvider.cs.example`
- [ ] **4.x** (опционально) `.resx` + `ResourceLocalizationProvider`
- [ ] **5.x** (постепенно) миграция hardcoded-строк
- [x] **T.1** Unit-тесты для `SR` → `tests/IntegrationFlow.Core.Tests`
- [x] **T.2** Сборка net8.0 + netstandard2.0

---

## Пример использования после внедрения

```csharp
// Core — без ELMA
throw new ArgumentNullException(
    responseString,
    SR.T("Получен пустой ответ при запросе {0}", Configuration.Url));

// ELMA host — при старте
SR.Configure(new ElmaLocalizationProvider());

// Standalone host — при старте (опционально)
SR.Configure(new ResourceLocalizationProvider());
```
