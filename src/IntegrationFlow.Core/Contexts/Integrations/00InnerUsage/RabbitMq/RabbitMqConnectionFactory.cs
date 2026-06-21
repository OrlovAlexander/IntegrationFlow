using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq
{
    /// <summary>
    /// Создание <see cref="ConnectionFactory"/> для подключений RabbitMQ.
    /// </summary>
    internal static class RabbitMqConnectionFactory
    {
        public static ConnectionFactory Create(RabbitMqConnectionSettings settings)
        {
            return new ConnectionFactory
            {
                HostName = settings.HostName,
                Port = settings.Port,
                UserName = settings.UserName,
                Password = settings.Password,
                VirtualHost = settings.VirtualHost,
                AutomaticRecoveryEnabled = settings.AutomaticRecoveryEnabled,
                DispatchConsumersAsync = settings.DispatchConsumersAsync,
                ClientProvidedName = settings.ClientProvidedName
            };
        }
    }
}
