using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Cfg;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations
{
    /// <summary>
    /// Конфигурация публикации сообщений в RabbitMQ для SentAndForgot.
    /// </summary>
    public class RabbitMqPublishConfiguration : IConfiguration
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
        public string ClientProvidedName { get; set; } = "IntegrationFlow.RabbitMqPublisher";

        /// <summary>
        /// Цель публикации: очередь или exchange.
        /// </summary>
        public RabbitMqPublishTarget PublishTarget { get; set; } = RabbitMqPublishTarget.Queue;

        /// <summary>
        /// Имя очереди (обязательно при <see cref="PublishTarget"/> = <see cref="RabbitMqPublishTarget.Queue"/>).
        /// </summary>
        public string QueueName { get; set; } = string.Empty;

        /// <summary>
        /// Имя exchange (обязательно при <see cref="PublishTarget"/> = <see cref="RabbitMqPublishTarget.Exchange"/>).
        /// </summary>
        public string Exchange { get; set; } = string.Empty;

        /// <summary>
        /// Ключ маршрутизации для direct/topic exchange.
        /// </summary>
        public string RoutingKey { get; set; } = string.Empty;

        /// <summary>
        /// MIME-тип тела сообщения.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Сохранять сообщение на диск (DeliveryMode = 2).
        /// </summary>
        public bool Persistent { get; set; } = true;

        /// <summary>
        /// Флаг mandatory для BasicPublish.
        /// </summary>
        public bool Mandatory { get; set; }

        /// <summary>
        /// Включить publisher confirms для подтверждения приёма сообщения брокером.
        /// </summary>
        public bool PublisherConfirmsEnabled { get; set; } = true;

        /// <summary>
        /// Таймаут ожидания publisher confirm (секунды).
        /// </summary>
        public int ConfirmTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Проверять существование очереди/exchange перед публикацией (passive declare).
        /// </summary>
        public bool ValidateTopology { get; set; } = true;

        /// <summary>
        /// Переиспользовать TCP-подключение между вызовами интеграции (direct publish).
        /// </summary>
        public bool ReuseConnection { get; set; }

        /// <summary>
        /// Использовать TLS (AMQPS) при подключении к брокеру.
        /// </summary>
        public bool SslEnabled { get; set; }

        /// <summary>
        /// Имя сервера для проверки TLS-сертификата (SNI).
        /// </summary>
        public string SslServerName { get; set; } = string.Empty;

        /// <summary>
        /// Проверяет корректность конфигурации.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(HostName))
            {
                throw new InvalidOperationException("Не задан HostName для RabbitMQ publish.");
            }

            if (PublishTarget == RabbitMqPublishTarget.Queue)
            {
                if (string.IsNullOrWhiteSpace(QueueName))
                {
                    throw new InvalidOperationException("Не задано имя очереди RabbitMQ для PublishTarget=Queue.");
                }
            }
            else if (PublishTarget == RabbitMqPublishTarget.Exchange)
            {
                if (string.IsNullOrWhiteSpace(Exchange))
                {
                    throw new InvalidOperationException("Не задан exchange RabbitMQ для PublishTarget=Exchange.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Неизвестный PublishTarget: {PublishTarget}.");
            }
        }

        internal string GetPublishExchange()
        {
            return PublishTarget == RabbitMqPublishTarget.Queue
                ? string.Empty
                : Exchange;
        }

        internal string GetPublishRoutingKey()
        {
            return PublishTarget == RabbitMqPublishTarget.Queue
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
                DispatchConsumersAsync = false,
                SslEnabled = SslEnabled,
                SslServerName = SslServerName
            };
        }

        internal static RabbitMqConnectionSettings ToConnectionSettings(RabbitMqConfiguration configuration)
        {
            return new RabbitMqConnectionSettings
            {
                HostName = configuration.HostName,
                Port = configuration.Port,
                UserName = configuration.UserName,
                Password = configuration.Password,
                VirtualHost = configuration.VirtualHost,
                AutomaticRecoveryEnabled = configuration.AutomaticRecoveryEnabled,
                ClientProvidedName = configuration.ClientProvidedName,
                DispatchConsumersAsync = true,
                SslEnabled = configuration.SslEnabled,
                SslServerName = configuration.SslServerName
            };
        }
    }
}
