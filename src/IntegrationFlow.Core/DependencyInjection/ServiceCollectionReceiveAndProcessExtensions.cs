using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntegrationFlow.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a RabbitMQ ReceiveAndProcess listener as a hosted service (.NET 8+).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="profileName">RabbitMQ profile name from rabbitmq.json.</param>
    /// <param name="handleMessage">Business handler for incoming inbox messages.</param>
    public static IServiceCollection AddIntegrationFlowRabbitMqListener(
        this IServiceCollection services,
        string profileName,
        Action<InboxMessage> handleMessage)
    {
        if (handleMessage == null)
        {
            throw new ArgumentNullException(nameof(handleMessage));
        }

        return services.AddIntegrationFlowRabbitMqListener(
            profileName,
            _ => new DelegateInboxMessageProcessing(handleMessage));
    }

    /// <summary>
    /// Registers a RabbitMQ ReceiveAndProcess listener as a hosted service (.NET 8+).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="profileName">RabbitMQ profile name from rabbitmq.json.</param>
    /// <param name="createProcessing">Factory for inbox message handler (supports DI).</param>
    /// <param name="createDeduplicationStore">Optional deduplication store factory.</param>
    public static IServiceCollection AddIntegrationFlowRabbitMqListener(
        this IServiceCollection services,
        string profileName,
        Func<IServiceProvider, IInboxMessageProcessing> createProcessing,
        Func<IServiceProvider, IMessageDeduplicationStore>? createDeduplicationStore = null)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name is required.", nameof(profileName));
        }

        if (createProcessing == null)
        {
            throw new ArgumentNullException(nameof(createProcessing));
        }

#if NET8_0_OR_GREATER
        services.Add(ServiceDescriptor.Singleton<IHostedService>(sp =>
        {
            var logger = sp.GetRequiredService<IIntegrationLogger>();
            var metrics = sp.GetService<IIntegrationFlowMetrics>();
            var healthRegistry = sp.GetService<RabbitMqTransportHealthRegistry>();
            healthRegistry?.Register(RabbitMqTransportKind.Listener, profileName);
            var processing = createProcessing(sp);
            var deduplicationStore = createDeduplicationStore?.Invoke(sp);
            var options = ReceiveAndProcessHostedServiceOptions.CreateForProfile(
                profileName,
                logger,
                processing,
                deduplicationStore,
                metrics);
            return new ReceiveAndProcessHostedService(options, logger, healthRegistry);
        }));
#endif

        return services;
    }
}
