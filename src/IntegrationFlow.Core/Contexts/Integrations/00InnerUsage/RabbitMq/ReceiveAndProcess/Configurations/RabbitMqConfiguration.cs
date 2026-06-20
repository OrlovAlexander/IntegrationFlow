using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations
{
    /// <summary>
    /// Конфигурация подключения к очереди RabbitMQ.
    /// </summary>
    public abstract class RabbitMqConfiguration : IConfiguration
    {
        /// <inheritdoc />
        public bool Asynchronously { get; set; } = true;

        /// <summary>
        /// Хост брокера RabbitMQ.
        /// </summary>
        public string HostName { get; protected set; } = "localhost";

        /// <summary>
        /// Порт брокера RabbitMQ.
        /// </summary>
        public int Port { get; protected set; } = 5672;

        /// <summary>
        /// Имя пользователя.
        /// </summary>
        public string UserName { get; protected set; } = "guest";

        /// <summary>
        /// Пароль.
        /// </summary>
        public string Password { get; protected set; } = "guest";

        /// <summary>
        /// Виртуальный хост.
        /// </summary>
        public string VirtualHost { get; protected set; } = "/";

        /// <summary>
        /// Имя очереди для прослушивания.
        /// </summary>
        public string QueueName { get; protected set; } = string.Empty;

        /// <summary>
        /// Количество неподтверждённых сообщений, которые consumer может получить заранее.
        /// </summary>
        public ushort PrefetchCount { get; protected set; } = 1;

        /// <summary>
        /// Автоматическое восстановление соединения при разрыве.
        /// </summary>
        public bool AutomaticRecoveryEnabled { get; protected set; } = true;

        /// <summary>
        /// Имя клиента для идентификации подключения на стороне брокера.
        /// </summary>
        public string ClientProvidedName { get; protected set; } = "IntegrationFlow.RabbitMqListener";
    }
}
