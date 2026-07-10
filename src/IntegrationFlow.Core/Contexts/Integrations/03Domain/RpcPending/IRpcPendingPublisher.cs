using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;

/// <summary>
/// Publishes staged async RPC pending requests to a transport.
/// </summary>
public interface IRpcPendingPublisher : IDisposable
{
    RpcPendingTransportKind TransportKind { get; }

    void PublishPendingRequest(RpcPendingRequest request);
}
