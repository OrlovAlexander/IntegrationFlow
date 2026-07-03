using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Processors;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using Xunit;

namespace IntegrationFlow.Tests.ReceiveAndProcess;

public sealed class DefaultRabbitMqProcessorSideTests
{
    [Fact]
    public async Task DefaultProcessorSide_ThrowsWhenNoBusinessHandlerConfigured()
    {
        var logger = NullIntegrationLogger.Instance;
        var publisher = PublisherBase.Create<RabbitMqPublisher, InboxRabbitMqPublisherSide>(logger);
        var configuration = publisher.IntegrationPublisherSide.GetConfiguration(publisher, logger);
        var processor = publisher.IntegrationPublisherSide.GetProcessor(publisher, configuration, logger);

        await Assert.ThrowsAsync<NotImplementedException>(() =>
            processor.ProcessMessageAsync(new object(), CancellationToken.None));
    }
}
