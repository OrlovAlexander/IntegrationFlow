using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.InboxMessageProcessing
{
    /// <summary>
    /// Заглушка обработки входящего сообщения.
    /// </summary>
    internal sealed class NoOpInboxMessageProcessing : IInboxMessageProcessing
    {
        internal static NoOpInboxMessageProcessing Instance { get; } = new();

        private NoOpInboxMessageProcessing()
        {
        }

        /// <inheritdoc />
        public void ProcessInboxMessage(InboxMessage inboxMessage)
        {
        }
    }
}
