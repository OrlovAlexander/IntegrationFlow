using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationFlow.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a RabbitMQ ReceiveAndProcess listener as a hosted service (.NET 8+).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="profileName">RabbitMQ profile name from rabbitmq.json.</param>
    public static IServiceCollection AddIntegrationFlowRabbitMqListener(
        this IServiceCollection services,
        string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new System.ArgumentException("Profile name is required.", nameof(profileName));
        }

#if NET8_0_OR_GREATER
        services.AddHostedService(sp =>
        {
            var logger = sp.GetRequiredService<IIntegrationLogger>();
            var options = ReceiveAndProcessHostedServiceOptions.CreateForProfile(profileName, logger);
            return new ReceiveAndProcessHostedService(options, logger);
        });
#endif

        return services;
    }
}
