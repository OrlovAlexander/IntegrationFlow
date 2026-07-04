using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Maintenance
{
    /// <summary>
    /// Retention options for IntegrationFlow background cleanup.
    /// </summary>
    public sealed class IntegrationFlowMaintenanceOptions
    {
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromHours(1);

        public TimeSpan RpcPendingTerminalRetention { get; set; } = TimeSpan.FromDays(30);

        public int RpcPendingPurgeBatchSize { get; set; } = 200;

        public int ResponseCachePurgeBatchSize { get; set; } = 500;
    }
}
