using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Workers;
using IntegrationFlow.Contexts.Integrations._03Domain;
using Microsoft.Extensions.Hosting;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;

/// <summary>
/// Hosted-service wrapper for RabbitMQ ReceiveAndProcess listener.
/// </summary>
internal sealed class ReceiveAndProcessHostedService : BackgroundService
{
    private readonly ReceiveAndProcessHostedServiceOptions options;
    private readonly IIntegrationLogger logger;
    private readonly RabbitMqTransportHealthRegistry? healthRegistry;
    private readonly RabbitMqListenerWorker worker = new();

    public ReceiveAndProcessHostedService(
        ReceiveAndProcessHostedServiceOptions options,
        IIntegrationLogger logger,
        RabbitMqTransportHealthRegistry? healthRegistry = null)
    {
        this.options = options ?? throw new System.ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        this.healthRegistry = healthRegistry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await worker.RunAsync(
            options.Configuration,
            options.ProcessMessageAsync,
            logger,
            stoppingToken,
            metrics: options.Metrics,
            healthRegistry: healthRegistry);
    }
}
