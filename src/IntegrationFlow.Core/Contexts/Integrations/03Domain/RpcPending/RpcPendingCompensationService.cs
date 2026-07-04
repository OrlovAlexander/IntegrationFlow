using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    /// <summary>
    /// Dispatches compensation handlers for failed or timed-out async RPC pending requests.
    /// </summary>
    public sealed class RpcPendingCompensationService
    {
        private readonly IRpcPendingStore pendingStore;
        private readonly IReadOnlyList<IRpcCompensationHandler> handlers;
        private readonly IIntegrationLogger logger;

        public RpcPendingCompensationService(
            IRpcPendingStore pendingStore,
            IEnumerable<IRpcCompensationHandler> handlers,
            IIntegrationLogger logger)
        {
            this.pendingStore = pendingStore ?? throw new ArgumentNullException(nameof(pendingStore));
            this.handlers = handlers == null
                ? Array.Empty<IRpcCompensationHandler>()
                : new List<IRpcCompensationHandler>(handlers);
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessBatchAsync(int batchSize = 20, CancellationToken cancellationToken = default)
        {
            if (handlers.Count == 0)
            {
                return;
            }

            var candidates = await pendingStore
                .GetCompensationCandidatesAsync(batchSize, cancellationToken)
                .ConfigureAwait(false);

            foreach (var request in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var handler in handlers)
                {
                    try
                    {
                        if (await handler.TryCompensateAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            await pendingStore
                                .MarkCompensatedAsync(request.Id, cancellationToken)
                                .ConfigureAwait(false);
                            logger.LogInfo(
                                $"Rpc pending compensation. Request '{request.Id}' compensated via '{handler.GetType().Name}'.");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogException($"Rpc pending compensation. Failed for request '{request.Id}'.", ex);
                    }
                }
            }
        }
    }
}
