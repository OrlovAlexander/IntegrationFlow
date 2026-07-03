using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Processors;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Validator;
using Xunit;

namespace IntegrationFlow.Tests.ReceiveAndProcess;

public sealed class ProcessorBaseDeduplicationTests
{
    [Fact]
    public async Task ProcessMessageAsync_ReleasesLockOnException()
    {
        var store = new InMemoryMessageDeduplicationStore();
        ConfigureSide(store, shouldThrow: true);
        var processor = CreateProcessor();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessMessageAsync(new TestMessage("msg-1"), CancellationToken.None));

        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-1"));
    }

    [Fact]
    public async Task ProcessMessageAsync_MarksProcessedOnSuccess()
    {
        var store = new InMemoryMessageDeduplicationStore();
        ConfigureSide(store, shouldThrow: false);
        var processor = CreateProcessor();

        await processor.ProcessMessageAsync(new TestMessage("msg-2"), CancellationToken.None);

        Assert.Equal(DeduplicationBeginResult.AlreadyProcessed, await store.TryBeginProcessingAsync("msg-2"));
    }

    [Fact]
    public async Task ProcessMessageAsync_SkipsAlreadyProcessed()
    {
        var store = new InMemoryMessageDeduplicationStore();
        ConfigureSide(store, shouldThrow: false);
        var processor = CreateProcessor();

        await store.TryBeginProcessingAsync("msg-3");
        await store.MarkProcessedAsync("msg-3");

        await processor.ProcessMessageAsync(new TestMessage("msg-3"), CancellationToken.None);

        Assert.Equal(0, TestProcessorSide.ProcessCallCount);
    }

    [Fact]
    public async Task ProcessMessageAsync_ThrowsInProgressForParallelDelivery()
    {
        var store = new InMemoryMessageDeduplicationStore();
        ConfigureSide(store, shouldThrow: false);
        var processor = CreateProcessor();

        await store.TryBeginProcessingAsync("msg-4");

        await Assert.ThrowsAsync<MessageProcessingInProgressException>(() =>
            processor.ProcessMessageAsync(new TestMessage("msg-4"), CancellationToken.None));
    }

    [Fact]
    public void Create_ReturnsDifferentProcessorsForDifferentProfileCacheKeys()
    {
        var logger = NullIntegrationLogger.Instance;
        var inboxPublisher = PublisherBase.Create<RabbitMqPublisher, InboxRabbitMqPublisherSide>(logger);
        var ordersPublisher = PublisherBase.Create<RabbitMqPublisher, OrdersRabbitMqPublisherSide>(logger);

        var inboxConfig = inboxPublisher.IntegrationPublisherSide.GetConfiguration(inboxPublisher, logger);
        var ordersConfig = ordersPublisher.IntegrationPublisherSide.GetConfiguration(ordersPublisher, logger);

        var inboxProcessor = inboxPublisher.IntegrationPublisherSide.GetProcessor(inboxPublisher, inboxConfig, logger);
        var ordersProcessor = ordersPublisher.IntegrationPublisherSide.GetProcessor(ordersPublisher, ordersConfig, logger);

        Assert.NotSame(inboxProcessor, ordersProcessor);
    }

    private static void ConfigureSide(IMessageDeduplicationStore store, bool shouldThrow)
    {
        TestProcessorSide.CurrentStore = store;
        TestProcessorSide.ShouldThrow = shouldThrow;
        TestProcessorSide.ProcessCallCount = 0;
    }

    private static ProcessorBase CreateProcessor()
    {
        var logger = NullIntegrationLogger.Instance;
        var publisher = PublisherBase.Create<RabbitMqPublisher, InboxRabbitMqPublisherSide>(logger);
        var configuration = publisher.IntegrationPublisherSide.GetConfiguration(publisher, logger);

        return ProcessorBase.Create<RabbitMqProcessor, TestProcessorSide>(
            publisher,
            configuration,
            logger,
            Guid.NewGuid().ToString("N"));
    }

    private sealed class TestMessage : IIntegrationMessageMetadata
    {
        public TestMessage(string messageId) => MessageId = messageId;

        public string MessageId { get; }
    }

    internal sealed class TestProcessorSide : IntegrationProcessorSideBase
    {
        internal static IMessageDeduplicationStore CurrentStore { get; set; } = null!;

        internal static bool ShouldThrow { get; set; }

        internal static int ProcessCallCount { get; set; }

        public override IValidator GetValidator(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger)
            => null!;

        public override ILogging GetLogging(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger)
            => null!;

        public override IInboxMessageFailedProcessing GetInboxMessageFailedProcessing(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger)
            => null!;

        public override IFormatterInboxMessage GetFormatterInboxMessage(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger)
            => null!;

        public override IInboxMessageProcessing GetInboxMessageProcessing(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger)
            => new CallbackInboxMessageProcessing(() =>
            {
                ProcessCallCount++;
                if (ShouldThrow)
                {
                    throw new InvalidOperationException("processing failed");
                }
            });

        public override IMessageDeduplicationStore GetMessageDeduplicationStore(
            PublisherBase publisher,
            IConfiguration configuration,
            IIntegrationLogger logger)
            => CurrentStore;
    }

    private sealed class CallbackInboxMessageProcessing : IInboxMessageProcessing
    {
        private readonly Action callback;

        public CallbackInboxMessageProcessing(Action callback) => this.callback = callback;

        public void ProcessInboxMessage(InboxMessage inboxMessage) => callback();
    }
}
