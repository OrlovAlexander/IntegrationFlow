namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait
{
    /// <summary>
    /// Константы request-reply для RabbitMQ.
    /// </summary>
    internal static class RabbitMqRequestReplyConstants
    {
        /// <summary>
        /// Адрес direct reply-to pseudo-очереди RabbitMQ.
        /// </summary>
        public const string DirectReplyToAddress = "amq.rabbitmq.reply-to";
    }
}
