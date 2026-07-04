using System;
using IntegrationFlow.Contexts.Integrations._03Domain.Maintenance;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntegrationFlow.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers async RPC pending compensation worker (.NET 8+).
    /// </summary>
    public static IServiceCollection AddIntegrationFlowRpcPendingCompensation(
        this IServiceCollection services,
        Action<RpcPendingCompensationOptions>? configure = null)
    {
        var options = new RpcPendingCompensationOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<RpcPendingCompensationService>();

#if NET8_0_OR_GREATER
        services.AddHostedService<RpcPendingCompensationBackgroundService>();
#endif

        return services;
    }

    /// <summary>
    /// Registers retention cleanup for rpc pending and response cache (.NET 8+).
    /// </summary>
    public static IServiceCollection AddIntegrationFlowMaintenance(
        this IServiceCollection services,
        Action<IntegrationFlowMaintenanceOptions>? configure = null)
    {
        var options = new IntegrationFlowMaintenanceOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IntegrationFlowMaintenanceService>(sp =>
            new IntegrationFlowMaintenanceService(
                sp.GetRequiredService<IntegrationFlowMaintenanceOptions>(),
                sp.GetService<IRpcPendingStore>(),
                sp.GetService<IRequestReplyResponseStore>()));

#if NET8_0_OR_GREATER
        services.AddHostedService<IntegrationFlowMaintenanceBackgroundService>();
#endif

        return services;
    }
}
