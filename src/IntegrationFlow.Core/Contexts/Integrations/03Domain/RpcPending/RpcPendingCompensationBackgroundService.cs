#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    internal sealed class RpcPendingCompensationBackgroundService : BackgroundService
    {
        private readonly RpcPendingCompensationService compensationService;
        private readonly RpcPendingCompensationOptions options;

        public RpcPendingCompensationBackgroundService(
            RpcPendingCompensationService compensationService,
            RpcPendingCompensationOptions options)
        {
            this.compensationService = compensationService;
            this.options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await compensationService
                    .ProcessBatchAsync(options.BatchSize, stoppingToken)
                    .ConfigureAwait(false);
                await Task.Delay(options.PollingInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
#endif
