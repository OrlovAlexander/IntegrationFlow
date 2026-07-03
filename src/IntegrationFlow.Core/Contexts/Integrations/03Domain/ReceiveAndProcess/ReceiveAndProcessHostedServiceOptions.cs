using System;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Processors;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;

/// <summary>
/// Options for <see cref="ReceiveAndProcessHostedService"/>.
/// </summary>
internal sealed class ReceiveAndProcessHostedServiceOptions
{
    public RabbitMqConfiguration Configuration { get; set; } = null!;

    public Func<object, Task> ProcessMessageAsync { get; set; } = null!;

    internal static ReceiveAndProcessHostedServiceOptions CreateForProfile(
        string profileName,
        IIntegrationLogger logger,
        IInboxMessageProcessing inboxMessageProcessing,
        IMessageDeduplicationStore? deduplicationStore = null,
        IIntegrationFlowMetrics? metrics = null)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name is required.", nameof(profileName));
        }

        if (inboxMessageProcessing == null)
        {
            throw new ArgumentNullException(nameof(inboxMessageProcessing));
        }

        var publisherSide = new NamedRabbitMqIntegrationPublisherSide(profileName);
        return CreateHosted(publisherSide, logger, inboxMessageProcessing, deduplicationStore, metrics);
    }

    private static ReceiveAndProcessHostedServiceOptions CreateHosted(
        NamedRabbitMqIntegrationPublisherSide publisherSide,
        IIntegrationLogger logger,
        IInboxMessageProcessing inboxMessageProcessing,
        IMessageDeduplicationStore? deduplicationStore,
        IIntegrationFlowMetrics? metrics)
    {
        var publisher = PublisherBase.Create<RabbitMqPublisher>(logger, publisherSide);
        publisher.Metrics = metrics;
        var configuration = publisherSide.GetConfiguration(publisher, logger);

        if (configuration is not RabbitMqConfiguration rabbitMqConfiguration)
        {
            throw new InvalidOperationException(
                $"Expected {nameof(RabbitMqConfiguration)} for profile listener registration.");
        }

        var processorSide = new HostedRabbitMqIntegrationProcessorSide(
            inboxMessageProcessing,
            deduplicationStore);

        var processor = ProcessorBase.CreateWithProcessorSide<RabbitMqProcessor>(
            publisher,
            configuration,
            logger,
            processorSide,
            $"{publisherSide.GetPublisherCacheKey()}|{inboxMessageProcessing.GetType().FullName}");

        return new ReceiveAndProcessHostedServiceOptions
        {
            Configuration = rabbitMqConfiguration,
            ProcessMessageAsync = message => processor.ProcessMessageAsync(message)
        };
    }
}
