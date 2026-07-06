namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Configurations;

/// <summary>
/// Общие параметры TCP-подключения к брокеру RabbitMQ.
/// </summary>
public interface IRabbitMqConnectionConfiguration
{
    string HostName { get; set; }

    int Port { get; set; }

    string UserName { get; set; }

    string Password { get; set; }

    string VirtualHost { get; set; }

    bool AutomaticRecoveryEnabled { get; set; }

    string ClientProvidedName { get; set; }

    bool SslEnabled { get; set; }

    string SslServerName { get; set; }
}
