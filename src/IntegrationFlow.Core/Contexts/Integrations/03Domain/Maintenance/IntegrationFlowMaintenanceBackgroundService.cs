#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.Maintenance;
using Microsoft.Extensions.Hosting;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Maintenance
{
    internal sealed class IntegrationFlowMaintenanceBackgroundService : BackgroundService
    {
        private readonly IntegrationFlowMaintenanceService maintenanceService;
        private readonly IntegrationFlowMaintenanceOptions options;

        public IntegrationFlowMaintenanceBackgroundService(
            IntegrationFlowMaintenanceService maintenanceService,
            IntegrationFlowMaintenanceOptions options)
        {
            this.maintenanceService = maintenanceService;
            this.options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await maintenanceService.RunCleanupAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(options.PollingInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
#endif
