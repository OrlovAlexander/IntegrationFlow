namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations
{
    /// <summary>
    /// Режим ожидания RPC-ответа.
    /// </summary>
    public enum RabbitMqReplyMode
    {
        /// <summary>
        /// Встроенная pseudo-очередь RabbitMQ <c>amq.rabbitmq.reply-to</c>.
        /// </summary>
        DirectReplyTo = 0,

        /// <summary>
        /// Exclusive auto-delete очередь, объявляемая клиентом.
        /// </summary>
        ExclusiveQueue = 1
    }
}
