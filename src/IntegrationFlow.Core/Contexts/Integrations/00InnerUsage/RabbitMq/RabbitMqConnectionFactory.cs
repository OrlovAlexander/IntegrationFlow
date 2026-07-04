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
            var factory = new ConnectionFactory
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

            if (settings.SslEnabled)
            {
                factory.Ssl.Enabled = true;
                if (!string.IsNullOrWhiteSpace(settings.SslServerName))
                {
                    factory.Ssl.ServerName = settings.SslServerName;
                }
            }

            return factory;
        }
    }
}
