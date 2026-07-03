using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication
{
    /// <summary>
    /// Сообщение уже обрабатывается; повторная доставка должна быть отложена (nack requeue).
    /// </summary>
    public sealed class MessageProcessingInProgressException : Exception
    {
        public MessageProcessingInProgressException(string messageId)
            : base($"Message '{messageId}' is already being processed.")
        {
            MessageId = messageId ?? string.Empty;
        }

        /// <summary>
        /// Идентификатор сообщения.
        /// </summary>
        public string MessageId { get; }
    }
}
