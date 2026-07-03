using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Validator;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess;

/// <summary>
/// Processor side for hosted listener with explicit inbox handler.
/// </summary>
internal sealed class HostedRabbitMqIntegrationProcessorSide : IntegrationProcessorSideBase
{
    private readonly IInboxMessageProcessing inboxMessageProcessing;
    private readonly IMessageDeduplicationStore? deduplicationStore;

    internal HostedRabbitMqIntegrationProcessorSide(
        IInboxMessageProcessing inboxMessageProcessing,
        IMessageDeduplicationStore? deduplicationStore = null)
    {
        this.inboxMessageProcessing = inboxMessageProcessing
            ?? throw new System.ArgumentNullException(nameof(inboxMessageProcessing));
        this.deduplicationStore = deduplicationStore;
    }

    public override IValidator GetValidator(
        PublisherBase publisher,
        IConfiguration configuration,
        IIntegrationLogger logger)
        => null!;

    public override ILogging GetLogging(
        PublisherBase publisher,
        IConfiguration configuration,
        IIntegrationLogger logger)
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
        => inboxMessageProcessing;

    public override IMessageDeduplicationStore? GetMessageDeduplicationStore(
        PublisherBase publisher,
        IConfiguration configuration,
        IIntegrationLogger logger)
        => deduplicationStore;
}
