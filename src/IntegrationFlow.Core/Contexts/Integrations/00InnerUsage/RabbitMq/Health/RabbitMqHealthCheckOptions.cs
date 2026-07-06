namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;

/// <summary>
/// Options for RabbitMQ transport health checks.
/// </summary>
public sealed class RabbitMqHealthCheckOptions
{
    /// <summary>
    /// Listener/RPC correlation endpoints become unhealthy after this many consecutive reconnect attempts.
    /// </summary>
    public int MaxReconnectAttemptsBeforeUnhealthy { get; set; } = 5;

    /// <summary>
    /// Outbox relay becomes unhealthy after this many consecutive failed relay batches.
    /// </summary>
    public int OutboxRelayMaxConsecutiveFailures { get; set; } = 5;
}
