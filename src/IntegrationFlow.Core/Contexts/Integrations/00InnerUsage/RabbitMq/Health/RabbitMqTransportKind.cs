namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;

/// <summary>
/// RabbitMQ transport component kind tracked by health checks.
/// </summary>
public enum RabbitMqTransportKind
{
    Listener,
    OutboxRelay,
    RpcCorrelation
}
