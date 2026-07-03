namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess
{
    /// <summary>
    /// Метаданные входящего сообщения для идемпотентности и трассировки.
    /// </summary>
    public interface IIntegrationMessageMetadata
    {
        /// <summary>
        /// Идентификатор сообщения.
        /// </summary>
        string MessageId { get; }
    }
}
