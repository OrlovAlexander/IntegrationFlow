# План: замена `EleWise.ELMA.SR.T` на локализацию в IntegrationFlow

**Статус:** выполнено (этапы 1–4, T.1–T.3); этап 5 — частично  
**Дата:** 2026-06-19 (обновлено 2026-06-20)  
**Цель:** убрать зависимость Core от ELMA SDK, сохранив совместимый API локализации и возможность подключения ELMA-переводов на стороне хоста.

---

## Контекст (исходное состояние)

На момент начала работ `EleWise.ELMA.SR.T` использовался только в sample-коде:

- `src/IntegrationFlow.Core/Contexts/Integrations/00Samples/Transmitters/RESTSimpleTransmitter.cs` (2 вызова)

В `IntegrationFlow.Core.csproj` не было ссылки на ELMA SDK — код не собирался автономно. Остальной код использовал hardcoded-строки и `string.Format` без единого механизма i18n.

---

## Принятое решение

В Core введена **абстракция локализации** с API, совместимым с ELMA:

```
ILocalizationProvider  →  SR.T(format, args)  →  fallback: string.Format(CurrentCulture, ...)
```

ELMA-адаптер подключается **в host-проекте / плагине**, не в Core.

Standalone host получает переводы из встроенных `.resx` через `LocalizationBootstrap.UseEmbeddedResources()` или автоматически при вызове `AddIntegrationFlow()`.

---

## Этапы выполнения

### Этап 1. Инфраструктура локализации в Core ✅

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

**Критерий готовности:** Core компилируется без ELMA, API `SR.T(...)` доступен из любого слоя. — **выполнено**

---

### Этап 2. Замена вызовов в sample-коде ✅

**Задачи:**

1. В `RESTSimpleTransmitter.cs`:
   - Заменить `EleWise.ELMA.SR.T(...)` → `SR.T(...)`
   - Добавить `using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization`

2. Проверить, что в репозитории не осталось ссылок на `EleWise.ELMA.SR`

**Критерий готовности:** `grep "EleWise.ELMA.SR"` в Core — 0 совпадений (кроме XML-комментариев). — **выполнено**

---

### Этап 3. ELMA-адаптер (отдельный проект или host-слой) ✅

**Задачи:**

1. Создать адаптер (если есть ELMA host-проект):
   - Класс: `ElmaLocalizationProvider : ILocalizationProvider`
   - Реализация: `EleWise.ELMA.SR.T(format, args)`

2. При старте плагина / host:
   ```csharp
   SR.Configure(new ElmaLocalizationProvider());
   ```

**Примечание:** ELMA host-проекта в репозитории нет — адаптер зафиксирован в `docs/examples/ElmaLocalizationProvider.cs.example`.

**Критерий готовности:** при запуске в ELMA переводы берутся из ELMA Translation; вне ELMA — fallback на исходный текст или `.resx`. — **выполнено (пример); ручная проверка в ELMA — не выполнена**

**Важно для ELMA host:** если используется `AddIntegrationFlow()`, он регистрирует `ResourceLocalizationProvider`. Для ELMA вызовите `SR.Configure(new ElmaLocalizationProvider())` **после** `AddIntegrationFlow()`.

---

### Этап 4. Standalone-локализация через `.resx` ✅

**Задачи:**

1. Добавить `IntegrationFlowResources.resx` (+ `IntegrationFlowResources.en.resx`) — **61 ключ**
2. Реализовать `ResourceLocalizationProvider`:
   - ключ = исходная строка (как в ELMA Orchard Localizer)
   - fallback на ключ, если перевод не найден
3. Bootstrap-хелпер `LocalizationBootstrap.UseEmbeddedResources()`:
   - Путь: `src/IntegrationFlow.Core/Contexts/Integrations/01Infrastructure/Localization/LocalizationBootstrap.cs`
   - Вызывается автоматически из `AddIntegrationFlow()`

**Критерий готовности:** Core работает с переводами без ELMA SDK. — **выполнено**

---

### Этап 5. (Постепенно) Миграция hardcoded-строк — частично ✅/⬜

Не блокирует этапы 1–4. Выполнять по мере появления переводов.

**Мигрировано (через `SR.T`, ключи в `.resx`):**

| Файл | Статус |
|------|--------|
| `ListenerBase.cs` | ✅ |
| `ProcessorBase.cs` | ✅ |
| `PublisherBase.cs` | ✅ |
| `SentAndWaitIntegration.cs` | ✅ |
| `SentAndForgotIntegration.cs` | ✅ |
| `RESTSimpleTransmitter.cs` | ✅ (пользовательские сообщения об ошибках) |

**Осталось (низкий приоритет):**

| Файл | Примеры строк |
|------|---------------|
| `RESTSimpleTransmitter.cs` | отладочные логи: `"RESTSimpleTrnasmiter - Trnasmit"`, `"Ответ: {0}"`, `"WebException"` |
| `TypeCollection.cs` | `"ReceiveAndProcess - TypeCollection - Добавить Публикатор '{0}'"` |
| `ObjectsComparerService/*` | внутренние исключения: `"Unsupported Type"`, `"Invalid type"` |

**Правило:** новые пользовательские сообщения — только через `SR.T(...)`.

---

## Структура файлов (итог)

```
src/IntegrationFlow.Core/
  Contexts/Integrations/01Infrastructure/Localization/
    ILocalizationProvider.cs
    SR.cs
    ResourceLocalizationProvider.cs
    LocalizationBootstrap.cs
    IntegrationFlowResources.resx
    IntegrationFlowResources.en.resx
    IntegrationFlowResources.Designer.cs

  Contexts/Integrations/00Samples/Transmitters/
    RESTSimpleTransmitter.cs

  DependencyInjection/
    ServiceCollectionExtensions.cs   ← вызывает LocalizationBootstrap

docs/
  plans/localization-sr-replacement.md
  examples/ElmaLocalizationProvider.cs.example

tests/IntegrationFlow.Core.Tests/Localization/
  SRTests.cs
  ResourceLocalizationProviderTests.cs
  LocalizationBootstrapTests.cs
  LocalizationTestCollection.cs
```

---

## Тестирование

### Unit-тесты ✅

1. `SR.T` без провайдера → возвращает `string.Format` с `CurrentCulture`
2. `SR.T` с mock-провайдером → вызывает провайдер с теми же аргументами
3. `SR.Configure` → последующие вызовы идут через новый провайдер
4. `ResourceLocalizationProvider` → en-перевод, форматирование, fallback
5. `LocalizationBootstrap.UseEmbeddedResources()` → подключает `.resx`
6. `AddIntegrationFlow()` → автоматически регистрирует embedded resources

**Результат:** 8 тестов, все проходят.

### Ручная проверка

- [x] Сборка `IntegrationFlow.Core` (net8.0 и netstandard2.0)
- [x] Нет ссылок на `EleWise.ELMA` в Core
- [x] Sample `RESTSimpleTransmitter` компилируется
- [x] Bootstrap через `AddIntegrationFlow()` подхватывает en-переводы
- [ ] (При наличии ELMA host) переводы подхватываются после `SR.Configure(new ElmaLocalizationProvider())`

---

## Риски и ограничения

| Риск | Митигация |
|------|-----------|
| Статический `SR.Configure` — глобальное состояние | Допустимо для совместимости с ELMA API; в тестах сбрасывать провайдер через `SR.Configure(null)` |
| `AddIntegrationFlow()` перезаписывает провайдер | ELMA host вызывает `SR.Configure(new ElmaLocalizationProvider())` после DI-регистрации |
| Ключ = полная русская строка | Соответствует ELMA Orchard Localizer; не менять формат ключей при миграции |
| netstandard2.0 vs net8.0 | Не добавлять зависимости, специфичные только для net8.0, в Core |

---

## Чеклист выполнения

- [x] **1.1** Создать `ILocalizationProvider`
- [x] **1.2** Создать `SR` с fallback
- [x] **2.1** Заменить вызовы в `RESTSimpleTransmitter.cs`
- [x] **2.2** Убедиться: 0 ссылок на `EleWise.ELMA.SR` в Core
- [x] **3.1** Пример `ElmaLocalizationProvider` → `docs/examples/ElmaLocalizationProvider.cs.example`
- [x] **4.1** `.resx` + `IntegrationFlowResources.en.resx` (61 ключ)
- [x] **4.2** `ResourceLocalizationProvider`
- [x] **4.3** `LocalizationBootstrap.UseEmbeddedResources()`
- [x] **4.4** Авторегистрация в `AddIntegrationFlow()`
- [x] **5.x** Миграция ключевых доменных файлов (см. таблицу выше)
- [ ] **5.x** Оставшиеся отладочные/внутренние строки (низкий приоритет)
- [x] **T.1** Unit-тесты для `SR`
- [x] **T.2** Unit-тесты для `ResourceLocalizationProvider`
- [x] **T.3** Unit-тесты для `LocalizationBootstrap` / `AddIntegrationFlow`
- [x] **T.4** Сборка net8.0 + netstandard2.0

---

## Пример использования

```csharp
// Core — без ELMA
throw new ArgumentNullException(
    responseString,
    SR.T("Получен пустой ответ при запросе {0}", Configuration.Url));

// Standalone host — явная регистрация
LocalizationBootstrap.UseEmbeddedResources();

// Standalone host — через DI (регистрация происходит автоматически)
services.AddIntegrationFlow();

// ELMA host — при старте (после AddIntegrationFlow, если используется DI)
SR.Configure(new ElmaLocalizationProvider());
```
