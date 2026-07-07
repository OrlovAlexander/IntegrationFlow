using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations
{
    /// <summary>
    /// Конфигурация request-reply через RabbitMQ для SentAndWait.
    /// </summary>
    public sealed class RabbitMqRequestReplyConfiguration : IConfiguration, IRabbitMqConnectionConfiguration
    {
        /// <summary>
        /// Имя профиля подключения в rabbitmq.json.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Хост брокера RabbitMQ.
        /// </summary>
        public string HostName { get; set; } = "localhost";

        /// <summary>
        /// Порт брокера RabbitMQ.
        /// </summary>
        public int Port { get; set; } = 5672;

        /// <summary>
        /// Имя пользователя.
        /// </summary>
        public string UserName { get; set; } = "guest";

        /// <summary>
        /// Пароль.
        /// </summary>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// Виртуальный хост.
        /// </summary>
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// Автоматическое восстановление соединения при разрыве.
        /// </summary>
        public bool AutomaticRecoveryEnabled { get; set; } = true;

        /// <summary>
        /// Имя клиента для идентификации подключения на стороне брокера.
        /// </summary>
        public string ClientProvidedName { get; set; } = "IntegrationFlow.RabbitMqRpcClient";

        /// <summary>
        /// Цель RPC-запроса: очередь или exchange.
        /// </summary>
        public RabbitMqRequestReplyTarget RequestTarget { get; set; } = RabbitMqRequestReplyTarget.Queue;

        /// <summary>
        /// Имя очереди запросов (обязательно при <see cref="RequestTarget"/> = Queue).
        /// </summary>
        public string QueueName { get; set; } = string.Empty;

        /// <summary>
        /// Имя exchange (обязательно при <see cref="RequestTarget"/> = Exchange).
        /// </summary>
        public string Exchange { get; set; } = string.Empty;

        /// <summary>
        /// Ключ маршрутизации для direct/topic exchange.
        /// </summary>
        public string RoutingKey { get; set; } = string.Empty;

        /// <summary>
        /// Режим ожидания ответа.
        /// </summary>
        public RabbitMqReplyMode ReplyMode { get; set; } = RabbitMqReplyMode.DirectReplyTo;

        /// <summary>
        /// Таймаут ожидания ответа (секунды).
        /// </summary>
        public int ResponseTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// MIME-тип тела сообщения.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Сохранять request на диск (DeliveryMode = 2).
        /// </summary>
        public bool Persistent { get; set; } = true;

        /// <summary>
        /// AMQP priority (0–255) для RPC request. Не задано — свойство не выставляется явно.
        /// </summary>
        public byte? Priority { get; set; }

        /// <summary>
        /// TTL request в миллисекундах (basic.properties.expiration). Не задано — свойство не выставляется.
        /// </summary>
        public int? ExpirationMilliseconds { get; set; }

        /// <summary>
        /// Флаг mandatory для BasicPublish request.
        /// </summary>
        public bool Mandatory { get; set; }

        /// <summary>
        /// Включить publisher confirms для request publish.
        /// </summary>
        public bool PublisherConfirmsEnabled { get; set; } = true;

        /// <summary>
        /// Таймаут ожидания publisher confirm для request (секунды).
        /// </summary>
        public int ConfirmTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Флаг mandatory для server-side RPC reply publish.
        /// </summary>
        public bool ReplyMandatory { get; set; }

        /// <summary>
        /// Проверять существование очереди/exchange перед publish (passive declare).
        /// </summary>
        public bool ValidateTopology { get; set; } = true;

        /// <summary>
        /// Создавать queue/exchange при старте (active declare). Только для dev; в production — passive + IaC.
        /// </summary>
        public bool DeclareTopologyOnStartup { get; set; }

        /// <summary>
        /// Тип exchange при <see cref="DeclareTopologyOnStartup"/> и <see cref="RequestTarget"/> = Exchange.
        /// </summary>
        public string ExchangeType { get; set; } = RabbitMQ.Client.ExchangeType.Direct;

        /// <summary>
        /// Максимум одновременных in-flight RPC на одном transmitter (0 = без ограничения).
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 1;

        /// <summary>
        /// Переиспользовать TCP-подключение между вызовами интеграции.
        /// </summary>
        public bool ReuseConnection { get; set; }

        /// <summary>
        /// Переиспользовать publish-channel для RPC-ответов на server-side.
        /// </summary>
        public bool ReuseReplyConnection { get; set; } = true;

        /// <summary>
        /// Подтверждать reply вручную после успешной корреляции (manual ack на consume channel).
        /// </summary>
        public bool ManualReplyAck { get; set; }

        /// <summary>
        /// Использовать TLS (AMQPS) при подключении к брокеру.
        /// </summary>
        public bool SslEnabled { get; set; }

        /// <summary>
        /// Имя сервера для проверки TLS-сертификата (SNI).
        /// </summary>
        public string SslServerName { get; set; } = string.Empty;

        /// <summary>
        /// Режим request-reply: sync blocking или async outbox.
        /// </summary>
        public RabbitMqRequestReplyRequestMode RequestMode { get; set; } = RabbitMqRequestReplyRequestMode.Sync;

        /// <summary>
        /// Очередь для RPC-ответов при <see cref="RequestMode"/> = AsyncOutbox.
        /// </summary>
        public string ResponseQueueName { get; set; } = string.Empty;

        /// <summary>
        /// SLA ожидания ответа для async pending (секунды).
        /// </summary>
        public int PendingTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Проверяет корректность конфигурации.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(HostName))
            {
                throw new InvalidOperationException("Не задан HostName для RabbitMQ request-reply.");
            }

            if (ResponseTimeoutSeconds <= 0)
            {
                throw new InvalidOperationException("ResponseTimeoutSeconds должен быть больше 0.");
            }

            if (ConfirmTimeoutSeconds <= 0)
            {
                throw new InvalidOperationException("ConfirmTimeoutSeconds должен быть больше 0.");
            }

            if (RequestTarget == RabbitMqRequestReplyTarget.Queue)
            {
                if (string.IsNullOrWhiteSpace(QueueName))
                {
                    throw new InvalidOperationException("Не задано имя очереди RabbitMQ для RequestTarget=Queue.");
                }
            }
            else if (RequestTarget == RabbitMqRequestReplyTarget.Exchange)
            {
                if (string.IsNullOrWhiteSpace(Exchange))
                {
                    throw new InvalidOperationException("Не задан exchange RabbitMQ для RequestTarget=Exchange.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Неизвестный RequestTarget: {RequestTarget}.");
            }

            if (RequestMode == RabbitMqRequestReplyRequestMode.AsyncOutbox &&
                string.IsNullOrWhiteSpace(ResponseQueueName))
            {
                throw new InvalidOperationException(
                    "ResponseQueueName обязателен для RequestMode=AsyncOutbox.");
            }

            if (PendingTimeoutSeconds <= 0)
            {
                throw new InvalidOperationException("PendingTimeoutSeconds должен быть больше 0.");
            }

            RabbitMqBasicPropertiesMapper.ValidateMessageDeliveryOptions(ExpirationMilliseconds);
        }

        internal string GetRequestExchange()
        {
            return RequestTarget == RabbitMqRequestReplyTarget.Queue
                ? string.Empty
                : Exchange;
        }

        internal string GetRequestRoutingKey()
        {
            return RequestTarget == RabbitMqRequestReplyTarget.Queue
                ? QueueName
                : RoutingKey ?? string.Empty;
        }

        internal RabbitMqConnectionSettings ToConnectionSettings()
        {
            return new RabbitMqConnectionSettings
            {
                HostName = HostName,
                Port = Port,
                UserName = UserName,
                Password = Password,
                VirtualHost = VirtualHost,
                AutomaticRecoveryEnabled = AutomaticRecoveryEnabled,
                ClientProvidedName = ClientProvidedName,
                DispatchConsumersAsync = true,
                SslEnabled = SslEnabled,
                SslServerName = SslServerName
            };
        }

        internal TimeSpan GetResponseTimeout()
            => TimeSpan.FromSeconds(Math.Max(1, ResponseTimeoutSeconds));
    }
}
