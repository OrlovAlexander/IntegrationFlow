using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Validator;

namespace IntegrationFlow.IntegrationTests.Infrastructure;

internal sealed class EndToEndProcessorSide : IntegrationProcessorSideBase
{
    internal static IMessageDeduplicationStore? CurrentStore { get; set; }

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

    public override IMessageDeduplicationStore? GetMessageDeduplicationStore(
        PublisherBase publisher,
        IConfiguration configuration,
        IIntegrationLogger logger)
        => CurrentStore;
}

internal sealed class EndToEndTestMessage : IIntegrationMessageMetadata
{
    public EndToEndTestMessage(string messageId) => MessageId = messageId;

    public string MessageId { get; }
}

internal sealed class CallbackInboxMessageProcessing : IInboxMessageProcessing
{
    private readonly Action callback;

    public CallbackInboxMessageProcessing(Action callback) => this.callback = callback;

    public void ProcessInboxMessage(InboxMessage inboxMessage) => callback();
}
