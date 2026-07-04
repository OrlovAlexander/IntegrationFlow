namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations
{
    /// <summary>
    /// Режим SentAndWait request-reply через RabbitMQ.
    /// </summary>
    public enum RabbitMqRequestReplyRequestMode
    {
        /// <summary>
        /// Синхронный blocking RPC (DirectReplyTo / exclusive queue).
        /// </summary>
        Sync,

        /// <summary>
        /// Async request outbox + response queue correlation.
        /// </summary>
        AsyncOutbox
    }
}
