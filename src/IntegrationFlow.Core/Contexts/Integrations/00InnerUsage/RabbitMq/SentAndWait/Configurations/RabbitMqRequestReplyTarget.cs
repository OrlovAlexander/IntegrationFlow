namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations
{
    /// <summary>
    /// Цель отправки RPC-запроса в RabbitMQ.
    /// </summary>
    public enum RabbitMqRequestReplyTarget
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
