using System.Text;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Processors;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.IntegrationTests.Infrastructure;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(RabbitMqIntegrationCollection.Name)]
public sealed class ConsumerHandlerEndToEndTests : IAsyncLifetime
{
    private const string QueueName = "integration.consumer.e2e";

    private readonly RabbitMqContainerFixture rabbitMq = new();

    public Task InitializeAsync() => rabbitMq.InitializeAsync();

    public Task DisposeAsync() => rabbitMq.DisposeAsync();

    [Fact]
    public async Task HandleAsync_WithRealQueue_AcksAndEmptiesQueue()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        ConfigureProcessor(shouldThrow: false);
        PublishToQueue("payload-1", "msg-success");

        await HandleNextMessageAsync(new RabbitMqConfiguration { RequeueOnFailure = false });

        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        Assert.Null(channel.BasicGet(QueueName, autoAck: true));
        Assert.Equal(1, EndToEndProcessorSide.ProcessCallCount);
    }

    [Fact]
    public async Task HandleAsync_DuplicateMessageId_SkipsSecondDelivery()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        var dedupStore = new InMemoryMessageDeduplicationStore();
        ConfigureProcessor(dedupStore: dedupStore, shouldThrow: false);

        PublishToQueue("dup-1", "dup-msg-id");
        PublishToQueue("dup-2", "dup-msg-id");

        await HandleNextMessageAsync(new RabbitMqConfiguration());
        await HandleNextMessageAsync(new RabbitMqConfiguration());

        Assert.Equal(1, EndToEndProcessorSide.ProcessCallCount);
        Assert.Equal(DeduplicationBeginResult.AlreadyProcessed, await dedupStore.TryBeginProcessingAsync("dup-msg-id"));
    }

    [Fact]
    public async Task HandleAsync_ProcessFailure_NacksWithRequeue()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        ConfigureProcessor(shouldThrow: true);
        PublishToQueue("fail-payload", "msg-fail");

        await HandleNextMessageAsync(new RabbitMqConfiguration { RequeueOnFailure = true });

        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        var redelivered = channel.BasicGet(QueueName, autoAck: true);

        Assert.NotNull(redelivered);
        Assert.Equal("msg-fail", redelivered!.BasicProperties.MessageId);
    }

    private void ConfigureProcessor(IMessageDeduplicationStore? dedupStore = null, bool shouldThrow = false)
    {
        EndToEndProcessorSide.CurrentStore = dedupStore;
        EndToEndProcessorSide.ShouldThrow = shouldThrow;
        EndToEndProcessorSide.ProcessCallCount = 0;
    }

    private async Task HandleNextMessageAsync(RabbitMqConfiguration configuration)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: true, arguments: null);

        var delivery = channel.BasicGet(QueueName, autoAck: false);
        Assert.NotNull(delivery);

        var processor = CreateProcessor();
        var channelSync = new object();
        var acknowledgement = new RabbitMqChannelAcknowledgement(
            channelSync,
            () => channel,
            NullIntegrationLogger.Instance);
        var handler = new RabbitMqReceivedMessageHandler(
            message => processor.ProcessMessageAsync(message),
            acknowledgement,
            NullIntegrationLogger.Instance);

        var receivedMessage = new RabbitMqReceivedMessage(
            delivery!.Body.ToArray(),
            delivery.DeliveryTag,
            delivery.RoutingKey,
            delivery.BasicProperties?.MessageId,
            delivery.BasicProperties?.CorrelationId,
            delivery.BasicProperties?.ReplyTo,
            delivery.BasicProperties?.Headers);

        await handler.HandleAsync(receivedMessage, configuration, CancellationToken.None);
    }

    private static ProcessorBase CreateProcessor()
    {
        var logger = NullIntegrationLogger.Instance;
        var publisher = PublisherBase.Create<RabbitMqPublisher, InboxRabbitMqPublisherSide>(logger);
        var configuration = publisher.IntegrationPublisherSide.GetConfiguration(publisher, logger);

        return ProcessorBase.Create<RabbitMqProcessor, EndToEndProcessorSide>(
            publisher,
            configuration,
            logger,
            Guid.NewGuid().ToString("N"));
    }

    private void PublishToQueue(string body, string messageId)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: true, arguments: null);

        var properties = channel.CreateBasicProperties();
        properties.MessageId = messageId;
        channel.BasicPublish(string.Empty, QueueName, properties, Encoding.UTF8.GetBytes(body));
    }
}
