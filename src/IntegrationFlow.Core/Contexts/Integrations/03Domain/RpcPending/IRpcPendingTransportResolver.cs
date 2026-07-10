namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;

/// <summary>
/// Resolves RPC pending relay transport from REST or RabbitMQ request-reply configuration.
/// </summary>
public interface IRpcPendingTransportResolver
{
    IRpcPendingPublisher CreatePublisher(string profileName);
}
