namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Configurations;

/// <summary>
/// Переиспользуемый профиль подключения к брокеру RabbitMQ (секция <c>RabbitMqConnections</c>).
/// </summary>
public sealed class RabbitMqConnectionProfile
{
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
    public string ClientProvidedName { get; set; } = string.Empty;

    /// <summary>
    /// Использовать TLS (AMQPS) при подключении к брокеру.
    /// </summary>
    public bool SslEnabled { get; set; }

    /// <summary>
    /// Имя сервера для проверки TLS-сертификата (SNI).
    /// </summary>
    public string SslServerName { get; set; } = string.Empty;

    internal void ApplyTo(IRabbitMqConnectionConfiguration target)
    {
        target.HostName = HostName;
        target.Port = Port;
        target.UserName = UserName;
        target.Password = Password;
        target.VirtualHost = VirtualHost;
        target.AutomaticRecoveryEnabled = AutomaticRecoveryEnabled;

        if (!string.IsNullOrWhiteSpace(ClientProvidedName))
        {
            target.ClientProvidedName = ClientProvidedName;
        }

        target.SslEnabled = SslEnabled;
        target.SslServerName = SslServerName;
    }
}
