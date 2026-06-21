namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations
{
    /// <summary>
    /// Цель публикации сообщения в RabbitMQ.
    /// </summary>
    public enum RabbitMqPublishTarget
    {
        /// <summary>
        /// Публикация через default exchange напрямую в очередь.
        /// </summary>
        Queue = 0,

        /// <summary>
        /// Публикация в именованный exchange с routing key.
        /// </summary>
        Exchange = 1
    }
}
