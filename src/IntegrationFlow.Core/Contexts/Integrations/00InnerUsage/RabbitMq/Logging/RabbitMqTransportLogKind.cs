namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Logging;

internal enum RabbitMqTransportLogKind
{
    Listener,
    OutboxRelay,
    RpcCorrelation,
    Publish,
    RequestReply,
}
