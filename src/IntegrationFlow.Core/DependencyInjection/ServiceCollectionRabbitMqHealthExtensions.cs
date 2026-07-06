#if NET8_0_OR_GREATER
using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntegrationFlow.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers RabbitMQ transport health checks for listener, outbox relay and RPC correlation workers.
    /// </summary>
    public static IHealthChecksBuilder AddIntegrationFlowRabbitMqHealthChecks(
        this IServiceCollection services,
        Action<RabbitMqHealthCheckOptions>? configure = null)
    {
        services.AddOptions<RabbitMqHealthCheckOptions>();
        if (configure != null)
        {
            services.Configure(configure);
        }

        return services
            .AddHealthChecks()
            .AddCheck<RabbitMqListenerHealthCheck>(
                "integrationflow.rabbitmq.listener",
                tags: new[] { "integrationflow", "rabbitmq", "ready" })
            .AddCheck<RabbitMqOutboxRelayHealthCheck>(
                "integrationflow.rabbitmq.outbox_relay",
                tags: new[] { "integrationflow", "rabbitmq", "ready" })
            .AddCheck<RabbitMqRpcCorrelationHealthCheck>(
                "integrationflow.rabbitmq.rpc_correlation",
                tags: new[] { "integrationflow", "rabbitmq", "ready" });
    }
}
#endif
