# Брокеры для каркаса IntegrationFlow

Каркас **не привязан к конкретному брокеру** — он задаёт паттерны интеграции, а транспорт подключается через реализации. Сейчас готов только **RabbitMQ** для сценария **ReceiveAndProcess**; для **SentAndWait** / **SentAndForgot** уже есть REST-транспорт, но не брокер.

## Как устроен каркас

Для входящих сообщений (`ReceiveAndProcess`) нужно реализовать:

- `ListenerBase` — долгоживущий слушатель, который получает сообщения и вызывает `ProcessMessage`
- `PublisherBase` + `IntegrationPublisherSideBase` — сборка listener/processor/configuration
- `ProcessorBase` — бизнес-обработка через inbox pipeline

Текущая реализация RabbitMQ ожидает от брокера:

- очередь (consumer на `QueueName`)
- **manual ack** после успешной обработки
- prefetch / backpressure
- долгоживущее подключение с восстановлением

## Какие брокеры подходят

### Хорошо ложатся на ReceiveAndProcess (очередь + ack)

| Брокер | Почему подходит | На что обратить внимание |
|--------|-----------------|--------------------------|
| **RabbitMQ** | Уже реализован; классическая модель queue + consumer + ack | AMQP, очереди, routing |
| **Azure Service Bus** (Queue/Subscription) | Peek-lock → обработка → Complete/Abandon | Хорош для .NET/Azure, есть sessions, dead-letter |
| **Amazon SQS** | Long polling, visibility timeout ≈ ack | Простой managed-сервис, at-least-once |
| **Redis Streams** | Consumer groups + `XACK` | Лёгкий, если Redis уже в инфраструктуре |
| **NATS JetStream** | Durable consumer + explicit ack | Простой протокол, хорош для микросервисов |
| **Apache ActiveMQ Artemis** | JMS/AMQP-очереди | Enterprise, on-prem |
| **IBM MQ** | Классика для корпоративных интеграций | Дороже и сложнее в эксплуатации |

### Подходят, но модель другая (event log / streaming)

| Брокер | Когда выбирать | Отличие от RabbitMQ |
|--------|----------------|---------------------|
| **Apache Kafka** | Много событий, replay, аналитика, event sourcing | Offset commit вместо ack; consumer groups; сообщения хранятся |
| **Azure Event Hubs** | Высокая пропускная способность, Azure | Скорее поток событий, чем inbox-очередь |
| **Google Pub/Sub** | Managed cloud, pull/push подписки | Ack deadline, ordering через ordering key |
| **Pulsar** | Kafka-подобный + очереди | Гибче, но сложнее в развёртывании |

Для **inbox-паттерна** (получил → обработал → подтвердил) чаще удобнее **очереди**, чем чистый event log.

### Подходят для исходящих сценариев (SentAndWait / SentAndForgot)

Сейчас исходящий транспорт — это `ITransmitter`, пример — REST.

| Брокер | SentAndForgot | SentAndWait |
|--------|---------------|-------------|
| **RabbitMQ** | Publish в очередь/topic | Request-reply (`reply-to`, correlation id) |
| **Kafka** | Produce в topic | Request-reply через reply topic + correlation id |
| **Azure Service Bus** | Send to queue/topic | Sessions + reply-to |
| **SQS** | SendMessage | Сложнее для sync reply; обычно через отдельную reply-очередь |
| **NATS** | Publish | Request-reply из коробки |

## Практические рекомендации по выбору

**1. Универсальный on-prem / self-hosted**

- **RabbitMQ** — уже есть в проекте, хорош для inbox, routing, request-reply
- **Kafka** — если нужны потоки событий, replay, интеграция с data pipeline

**2. Microsoft / Azure stack**

- **Azure Service Bus** — лучший fit для .NET-приложений в Azure
- **Event Hubs** — если это именно поток событий, а не inbox

**3. AWS**

- **SQS + SNS** — простые очереди и pub/sub
- **Amazon MQ (RabbitMQ/ActiveMQ)** — если нужен именно AMQP/JMS

**4. Лёгкая инфраструктура**

- **Redis Streams** — минимальный overhead
- **NATS JetStream** — простой и быстрый для service-to-service

**5. Enterprise / банки / госсектор**

- **IBM MQ**, **ActiveMQ Artemis** — часто уже есть в ландшафте

## Что важно при добавлении нового брокера

Для каждого брокера в каркасе обычно создаётся папка по образцу RabbitMQ:

```
00InnerUsage/{BrokerName}/ReceiveAndProcess/
  Configurations/
  Listeners/
  Publishers/
  Processors/
  Messages/
  {Broker}IntegrationPublisherSideBase.cs
```

Минимальные требования к адаптеру:

1. **Listener** — blocking/long-poll loop в `Listen()`
2. **Ack/nack** — подтверждение только после успешной обработки
3. **Idempotency** — большинство брокеров дают at-least-once
4. **Конфигурация** — наследник `IConfiguration` с параметрами подключения
5. **Модель сообщения** — свой `*ReceivedMessage`, передаваемый в `ProcessMessage(object)`

## Итог

Для этого каркаса подходят практически все брокеры с моделью **«очередь/подписка → получение → подтверждение»**. Самые естественные кандидаты после RabbitMQ:

1. **Azure Service Bus** — если стек Microsoft
2. **Kafka** — если нужен event streaming
3. **SQS** — если AWS
4. **Redis Streams / NATS** — если нужна лёгкость
5. **IBM MQ / Artemis** — если enterprise/on-prem

REST уже покрывает синхронные интеграции; брокеры лучше использовать для **асинхронного ReceiveAndProcess** и **fire-and-forget / async request-reply**.
