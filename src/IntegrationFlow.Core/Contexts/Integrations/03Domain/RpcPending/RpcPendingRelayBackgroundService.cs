#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    /// <summary>
    /// Background worker relaying pending async RPC requests.
    /// </summary>
    internal sealed class RpcPendingRelayBackgroundService : BackgroundService
    {
        private readonly RpcPendingRelayService relayService;
        private readonly RpcPendingRelayOptions options;

        public RpcPendingRelayBackgroundService(RpcPendingRelayService relayService, RpcPendingRelayOptions options)
        {
            this.relayService = relayService;
            this.options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await relayService.RelayBatchAsync(options.BatchSize, stoppingToken).ConfigureAwait(false);
                await Task.Delay(options.PollingInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
#endif
