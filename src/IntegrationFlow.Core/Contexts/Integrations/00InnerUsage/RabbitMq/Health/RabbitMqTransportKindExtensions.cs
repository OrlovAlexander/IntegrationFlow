namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;

internal static class RabbitMqTransportKindExtensions
{
    internal static string ToMetricKind(this RabbitMqTransportKind kind)
        => kind switch
        {
            RabbitMqTransportKind.Listener => "listener",
            RabbitMqTransportKind.OutboxRelay => "outbox_relay",
            RabbitMqTransportKind.RpcCorrelation => "rpc_correlation",
            _ => kind.ToString().ToLowerInvariant()
        };
}
