#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
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
    private readonly RabbitMqListenerWorker worker = new();

    public ReceiveAndProcessHostedService(
        ReceiveAndProcessHostedServiceOptions options,
        IIntegrationLogger logger)
    {
        this.options = options ?? throw new System.ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => worker.RunAsync(
            options.Configuration,
            options.ProcessMessageAsync,
            logger,
            stoppingToken,
            metrics: options.Metrics);
}
#endif
