namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;

/// <summary>
/// Connection lifecycle status for a RabbitMQ transport endpoint.
/// </summary>
public enum RabbitMqTransportConnectionStatus
{
    Unknown,
    Starting,
    Connected,
    Reconnecting,
    Disconnected,
    Stopped
}
