using System;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntegrationFlow.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers outbox relay services and optional background worker (.NET 8+).
    /// </summary>
    public static IServiceCollection AddIntegrationFlowOutboxRelay(
        this IServiceCollection services,
        Action<OutboxRelayOptions> configure = null)
    {
        var options = new OutboxRelayOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<OutboxRelayService>();

#if NET8_0_OR_GREATER
        services.AddHostedService<OutboxRelayBackgroundService>();
#endif

        return services;
    }
}
