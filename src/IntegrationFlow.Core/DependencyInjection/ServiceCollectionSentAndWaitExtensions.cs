#if NET8_0_OR_GREATER
using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntegrationFlow.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a hosted RabbitMQ RPC server (listener on request queue + reply publisher).
    /// Uses profile from <c>RabbitMqRequestReply</c> section in rabbitmq.json.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="requestReplyProfileName">Profile name in RabbitMqRequestReply section.</param>
    /// <param name="buildResponseAsync">Optional response builder; default echoes request body.</param>
    /// <param name="createResponseStore">Optional idempotent response cache factory.</param>
    public static IServiceCollection AddIntegrationFlowRabbitMqRpcServer(
        this IServiceCollection services,
        string requestReplyProfileName,
        Func<RabbitMqReceivedMessage, System.Threading.Tasks.Task<string>>? buildResponseAsync = null,
        Func<IServiceProvider, IRequestReplyResponseStore?>? createResponseStore = null)
    {
        if (string.IsNullOrWhiteSpace(requestReplyProfileName))
        {
            throw new ArgumentException("Request-reply profile name is required.", nameof(requestReplyProfileName));
        }

        services.AddHostedService(sp =>
        {
            var logger = sp.GetRequiredService<IIntegrationLogger>();
            var metrics = sp.GetService<IIntegrationFlowMetrics>();
            var healthRegistry = sp.GetService<RabbitMqTransportHealthRegistry>();
            healthRegistry?.Register(RabbitMqTransportKind.Listener, requestReplyProfileName);

            var processing = new RabbitMqRpcServerInboxMessageProcessing(
                requestReplyProfileName,
                buildResponseAsync,
                createResponseStore?.Invoke(sp));

            var options = ReceiveAndProcessHostedServiceOptions.CreateForRequestReplyProfile(
                requestReplyProfileName,
                logger,
                processing,
                metrics);

            return new ReceiveAndProcessHostedService(options, logger, healthRegistry);
        });

        return services;
    }
}
#endif
