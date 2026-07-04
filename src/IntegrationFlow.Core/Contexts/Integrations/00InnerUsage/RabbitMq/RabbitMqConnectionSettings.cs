namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq
{
    /// <summary>
    /// Общие параметры подключения к брокеру RabbitMQ.
    /// </summary>
    internal sealed class RabbitMqConnectionSettings
    {
        public string HostName { get; set; } = "localhost";

        public int Port { get; set; } = 5672;

        public string UserName { get; set; } = "guest";

        public string Password { get; set; } = "guest";

        public string VirtualHost { get; set; } = "/";

        public bool AutomaticRecoveryEnabled { get; set; } = true;

        public string ClientProvidedName { get; set; } = string.Empty;

        public bool DispatchConsumersAsync { get; set; }

        public bool SslEnabled { get; set; }

        public string SslServerName { get; set; } = string.Empty;
    }
}
