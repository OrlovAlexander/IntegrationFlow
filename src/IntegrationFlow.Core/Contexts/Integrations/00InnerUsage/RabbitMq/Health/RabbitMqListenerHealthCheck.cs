#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;

internal sealed class RabbitMqListenerHealthCheck : IHealthCheck
{
    private readonly RabbitMqTransportHealthRegistry registry;
    private readonly RabbitMqHealthCheckOptions options;

    public RabbitMqListenerHealthCheck(
        RabbitMqTransportHealthRegistry registry,
        IOptions<RabbitMqHealthCheckOptions> options)
    {
        this.registry = registry;
        this.options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var endpoints = registry.GetRegisteredEndpoints(RabbitMqTransportKind.Listener);
        var result = RabbitMqTransportHealthEvaluator.EvaluateConnectionEndpoints(
            RabbitMqTransportKind.Listener,
            endpoints,
            options);
        return Task.FromResult(result);
    }
}
#endif
