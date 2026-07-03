#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Фоновый worker для relay outbox-сообщений.
    /// </summary>
    internal sealed class OutboxRelayBackgroundService : BackgroundService
    {
        private readonly OutboxRelayService relayService;
        private readonly OutboxRelayOptions options;

        public OutboxRelayBackgroundService(OutboxRelayService relayService, OutboxRelayOptions options)
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
