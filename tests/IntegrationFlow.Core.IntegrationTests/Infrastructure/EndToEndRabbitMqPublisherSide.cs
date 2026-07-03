using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Processors;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;

namespace IntegrationFlow.IntegrationTests.Infrastructure;

internal sealed class EndToEndRabbitMqPublisherSide : RabbitMqIntegrationPublisherSideBase
{
    private readonly string configurationName;

    public EndToEndRabbitMqPublisherSide(string configurationName)
    {
        if (string.IsNullOrWhiteSpace(configurationName))
        {
            throw new ArgumentException("Configuration name is required.", nameof(configurationName));
        }

        this.configurationName = configurationName;
    }

    protected override string ConfigurationName => configurationName;

    public override ProcessorBase GetProcessor(
        PublisherBase publisher,
        IConfiguration configuration,
        IIntegrationLogger logger)
        => ProcessorBase.Create<RabbitMqProcessor, EndToEndProcessorSide>(
            publisher,
            configuration,
            logger,
            GetPublisherCacheKey());
}
