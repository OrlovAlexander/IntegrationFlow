namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners
{
    /// <summary>
    /// Подтверждение или отклонение сообщения на стороне брокера.
    /// </summary>
    internal interface IRabbitMqMessageAcknowledgement
    {
        void Acknowledge(ulong deliveryTag);

        void NegativeAcknowledge(ulong deliveryTag, bool requeue);
    }
}
