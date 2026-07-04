using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    /// <summary>
    /// Options for async RPC pending compensation worker.
    /// </summary>
    public sealed class RpcPendingCompensationOptions
    {
        public int BatchSize { get; set; } = 20;

        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    }
}
