using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations
{
    /// <summary>
    /// Конфигурация подключения к очереди RabbitMQ.
    /// </summary>
    public class RabbitMqConfiguration : IConfiguration, IRabbitMqConnectionConfiguration
    {
        /// <inheritdoc />
        public bool Asynchronously { get; set; } = true;

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
        /// Имя очереди для прослушивания.
        /// </summary>
        public string QueueName { get; set; } = string.Empty;

        /// <summary>
        /// Количество неподтверждённых сообщений, которые consumer может получить заранее.
        /// </summary>
        public ushort PrefetchCount { get; set; } = 1;

        /// <summary>
        /// Автоматическое восстановление соединения при разрыве.
        /// </summary>
        public bool AutomaticRecoveryEnabled { get; set; } = true;

        /// <summary>
        /// Имя клиента для идентификации подключения на стороне брокера.
        /// </summary>
        public string ClientProvidedName { get; set; } = "IntegrationFlow.RabbitMqListener";

        /// <summary>
        /// Повторно ставить сообщение в очередь при ошибке обработки.
        /// </summary>
        public bool RequeueOnFailure { get; set; }

        /// <summary>
        /// Максимальное количество попыток доставки (0 = без ограничения).
        /// </summary>
        public int MaxRetryCount { get; set; }

        /// <summary>
        /// Использовать TLS (AMQPS) при подключении к брокеру.
        /// </summary>
        public bool SslEnabled { get; set; }

        /// <summary>
        /// Имя сервера для проверки TLS-сертификата (SNI).
        /// </summary>
        public string SslServerName { get; set; } = string.Empty;

        /// <summary>
        /// Проверять существование очереди перед consume (passive declare).
        /// </summary>
        public bool ValidateTopology { get; set; } = true;

        /// <summary>
        /// Создавать очередь при старте (active declare). Только для dev; в production — passive + IaC.
        /// </summary>
        public bool DeclareTopologyOnStartup { get; set; }

        /// <summary>
        /// Тег consumer для <c>basic.consume</c>. Пустая строка — сгенерировать на брокере.
        /// </summary>
        public string ConsumerTag { get; set; } = string.Empty;

        /// <summary>
        /// Exclusive consumer: только один consumer на очередь (single-active-consumer на classic queue).
        /// </summary>
        public bool Exclusive { get; set; }
    }
}
