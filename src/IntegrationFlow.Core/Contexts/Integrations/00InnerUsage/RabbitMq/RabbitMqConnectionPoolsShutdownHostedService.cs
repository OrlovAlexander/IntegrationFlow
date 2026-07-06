#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;

internal sealed class RabbitMqConnectionPoolsShutdownHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        RabbitMqConnectionPoolRegistry.DisposeAll();
        return Task.CompletedTask;
    }
}
#endif
