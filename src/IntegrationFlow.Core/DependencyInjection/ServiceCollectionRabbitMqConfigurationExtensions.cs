using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationFlow.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers RabbitMQ configuration overlay from host <see cref="IConfiguration"/>.
    /// Values override <c>rabbitmq.json</c> and environment variables.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Host configuration (appsettings, env, user secrets).</param>
    public static IServiceCollection AddIntegrationFlowRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        RabbitMqConfigurationComposition.OverlayConfiguration = configuration;
        return services;
    }
}
