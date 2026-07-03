using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners
{
    /// <summary>
    /// Ack/nack через канал RabbitMQ.
    /// </summary>
    internal sealed class RabbitMqChannelAcknowledgement : IRabbitMqMessageAcknowledgement
    {
        private readonly object channelSync;
        private readonly IIntegrationLogger logger;
        private readonly System.Func<IModel> channelAccessor;

        public RabbitMqChannelAcknowledgement(
            object channelSync,
            System.Func<IModel> channelAccessor,
            IIntegrationLogger logger)
        {
            this.channelSync = channelSync;
            this.channelAccessor = channelAccessor;
            this.logger = logger;
        }

        public void Acknowledge(ulong deliveryTag)
        {
            lock (channelSync)
            {
                var channel = channelAccessor();
                if (channel == null || !channel.IsOpen)
                {
                    logger.Log(SR.T("RabbitMQ listener. Канал недоступен для ack. DeliveryTag='{0}'.", deliveryTag));
                    return;
                }

                channel.BasicAck(deliveryTag, multiple: false);
            }
        }

        public void NegativeAcknowledge(ulong deliveryTag, bool requeue)
        {
            lock (channelSync)
            {
                var channel = channelAccessor();
                if (channel == null || !channel.IsOpen)
                {
                    logger.Log(SR.T("RabbitMQ listener. Канал недоступен для nack. DeliveryTag='{0}'.", deliveryTag));
                    return;
                }

                channel.BasicNack(deliveryTag, multiple: false, requeue: requeue);
            }
        }
    }
}
