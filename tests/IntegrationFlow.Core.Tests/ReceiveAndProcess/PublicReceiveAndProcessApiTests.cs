using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Processors;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;
using Xunit;

namespace IntegrationFlow.Core.Tests.ReceiveAndProcess;

public sealed class PublicReceiveAndProcessApiTests
{
    [Fact]
    public void RabbitMqIntegrationPublisherSideBase_CanBeExtendedFromExternalAssembly()
    {
        var side = new TestPublisherSide("Inbox");

        Assert.Equal("Inbox", side.GetProfileName());
        Assert.Contains("Inbox", side.GetPublisherCacheKey());
    }

    private sealed class TestPublisherSide : RabbitMqIntegrationPublisherSideBase
    {
        private readonly string configurationName;

        public TestPublisherSide(string configurationName)
        {
            this.configurationName = configurationName;
        }

        protected override string ConfigurationName => configurationName;

        public override ProcessorBase GetProcessor(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger)
            => ProcessorBase.Create<RabbitMqProcessor, TestProcessorSide>(
                publisher,
                configuration,
                logger,
                GetPublisherCacheKey());
    }

    private sealed class TestProcessorSide : IntegrationProcessorSideBase
    {
        public override Contexts.Integrations._03Domain.ReceiveAndProcess.Validator.IValidator GetValidator(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger)
            => null!;

        public override Contexts.Integrations._03Domain.ReceiveAndProcess.Logging.ILogging GetLogging(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger)
            => null!;

        public override Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing.IInboxMessageFailedProcessing GetInboxMessageFailedProcessing(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger)
            => null!;

        public override Contexts.Integrations._03Domain.ReceiveAndProcess.Formatter.IFormatterInboxMessage GetFormatterInboxMessage(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger)
            => null!;

        public override Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing.IInboxMessageProcessing GetInboxMessageProcessing(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger)
            => null!;
    }
}
