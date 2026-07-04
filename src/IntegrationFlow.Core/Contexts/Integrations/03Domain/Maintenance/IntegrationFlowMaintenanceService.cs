using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Maintenance
{
    /// <summary>
    /// Purges expired RPC response cache entries and old terminal rpc pending records.
    /// </summary>
    public sealed class IntegrationFlowMaintenanceService
    {
        private readonly IntegrationFlowMaintenanceOptions options;
        private readonly IRpcPendingStore? pendingStore;
        private readonly IRequestReplyResponseStore? responseStore;

        public IntegrationFlowMaintenanceService(
            IntegrationFlowMaintenanceOptions options,
            IRpcPendingStore? pendingStore = null,
            IRequestReplyResponseStore? responseStore = null)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.pendingStore = pendingStore;
            this.responseStore = responseStore;
        }

        public async Task RunCleanupAsync(CancellationToken cancellationToken = default)
        {
            if (responseStore != null)
            {
                _ = await responseStore.PurgeExpiredAsync(cancellationToken).ConfigureAwait(false);
            }

            if (pendingStore != null)
            {
                var terminalBefore = DateTimeOffset.UtcNow.Subtract(options.RpcPendingTerminalRetention);
                var purged = options.RpcPendingPurgeBatchSize;
                while (purged >= options.RpcPendingPurgeBatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    purged = await pendingStore
                        .PurgeTerminalAsync(terminalBefore, options.RpcPendingPurgeBatchSize, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }
}
