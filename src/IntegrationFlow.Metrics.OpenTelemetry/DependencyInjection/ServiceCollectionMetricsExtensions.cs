using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntegrationFlow.Metrics.OpenTelemetry.DependencyInjection;

/// <summary>
/// DI registration for IntegrationFlow OpenTelemetry metrics.
/// </summary>
public static class ServiceCollectionMetricsExtensions
{
    /// <summary>
    /// Registers <see cref="OpenTelemetryIntegrationFlowMetrics"/> as <see cref="IIntegrationFlowMetrics"/>.
    /// </summary>
    public static IServiceCollection AddIntegrationFlowOpenTelemetryMetrics(
        this IServiceCollection services,
        Action<IntegrationFlowMetricsOptions>? configure = null)
    {
        var options = new IntegrationFlowMetricsOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.RemoveAll<IIntegrationFlowMetrics>();
        services.AddSingleton<IIntegrationFlowMetrics, OpenTelemetryIntegrationFlowMetrics>();
        return services;
    }
}
