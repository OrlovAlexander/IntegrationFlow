using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq;

public sealed class PublisherBaseTypeCollectionTests
{
    [Fact]
    public void Create_ReturnsDifferentPublishersForDifferentSideTypes()
    {
        var logger = NullIntegrationLogger.Instance;

        var inboxPublisher = PublisherBase.Create<RabbitMqPublisher, InboxRabbitMqPublisherSide>(logger);
        var ordersPublisher = PublisherBase.Create<RabbitMqPublisher, OrdersRabbitMqPublisherSide>(logger);

        Assert.NotSame(inboxPublisher, ordersPublisher);
    }

    [Fact]
    public void Create_ReturnsSamePublisherForSameSideType()
    {
        var logger = NullIntegrationLogger.Instance;

        var firstPublisher = PublisherBase.Create<RabbitMqPublisher, InboxRabbitMqPublisherSide>(logger);
        var secondPublisher = PublisherBase.Create<RabbitMqPublisher, InboxRabbitMqPublisherSide>(logger);

        Assert.Same(firstPublisher, secondPublisher);
    }

    [Fact]
    public void Create_WithNamedSide_ReturnsDifferentPublishersForDifferentProfileNames()
    {
        var logger = NullIntegrationLogger.Instance;

        var inboxPublisher = PublisherBase.Create<RabbitMqPublisher>(
            logger,
            new NamedRabbitMqIntegrationPublisherSide("Inbox"));
        var ordersPublisher = PublisherBase.Create<RabbitMqPublisher>(
            logger,
            new NamedRabbitMqIntegrationPublisherSide("Orders"));

        Assert.NotSame(inboxPublisher, ordersPublisher);
    }

    [Fact]
    public void Create_WithNamedSide_ReturnsSamePublisherForSameProfileName()
    {
        var logger = NullIntegrationLogger.Instance;

        var firstPublisher = PublisherBase.Create<RabbitMqPublisher>(
            logger,
            new NamedRabbitMqIntegrationPublisherSide("Inbox"));
        var secondPublisher = PublisherBase.Create<RabbitMqPublisher>(
            logger,
            new NamedRabbitMqIntegrationPublisherSide("Inbox"));

        Assert.Same(firstPublisher, secondPublisher);
    }
}
