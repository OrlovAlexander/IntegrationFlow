#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;

internal sealed class RabbitMqRpcCorrelationHealthCheck : IHealthCheck
{
    private readonly RabbitMqTransportHealthRegistry registry;
    private readonly RabbitMqHealthCheckOptions options;

    public RabbitMqRpcCorrelationHealthCheck(
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
        var endpoints = registry.GetRegisteredEndpoints(RabbitMqTransportKind.RpcCorrelation);
        var result = RabbitMqTransportHealthEvaluator.EvaluateConnectionEndpoints(
            RabbitMqTransportKind.RpcCorrelation,
            endpoints,
            options);
        return Task.FromResult(result);
    }
}
#endif
