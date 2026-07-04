using System;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntegrationFlow.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers async RPC pending relay worker (.NET 8+).
    /// </summary>
    public static IServiceCollection AddIntegrationFlowRpcPendingRelay(
        this IServiceCollection services,
        Action<RpcPendingRelayOptions>? configure = null)
    {
        var options = new RpcPendingRelayOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<RpcPendingRelayService>();

#if NET8_0_OR_GREATER
        services.AddHostedService<RpcPendingRelayBackgroundService>();
#endif

        return services;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Registers response queue consumer for AsyncOutbox RPC profiles.
    /// </summary>
    public static IServiceCollection AddIntegrationFlowRabbitMqRpcResponseCorrelation(
        this IServiceCollection services)
    {
        services.AddHostedService<Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Response.RabbitMqRpcResponseCorrelationHostedService>();
        return services;
    }
#endif
}
