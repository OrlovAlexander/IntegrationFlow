using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners
{
    /// <summary>
    /// Обработка полученного сообщения с ack/nack после завершения process.
    /// </summary>
    internal sealed class RabbitMqReceivedMessageHandler
    {
        private readonly Func<object, Task> processMessageAsync;
        private readonly IRabbitMqMessageAcknowledgement acknowledgement;
        private readonly IIntegrationLogger logger;

        public RabbitMqReceivedMessageHandler(
            Func<object, Task> processMessageAsync,
            IRabbitMqMessageAcknowledgement acknowledgement,
            IIntegrationLogger logger)
        {
            this.processMessageAsync = processMessageAsync ?? throw new ArgumentNullException(nameof(processMessageAsync));
            this.acknowledgement = acknowledgement ?? throw new ArgumentNullException(nameof(acknowledgement));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task HandleAsync(
            RabbitMqReceivedMessage receivedMessage,
            RabbitMqConfiguration configuration,
            IDictionary<string, object> headers,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await processMessageAsync(receivedMessage).ConfigureAwait(false);
                acknowledgement.Acknowledge(receivedMessage.DeliveryTag);
                logger.Log(SR.T("RabbitMQ listener. Сообщение подтверждено. DeliveryTag='{0}'.", receivedMessage.DeliveryTag));
            }
            catch (MessageProcessingInProgressException ex)
            {
                logger.Log(SR.T(
                    "RabbitMQ listener. Сообщение обрабатывается параллельно. DeliveryTag='{0}', MessageId='{1}'.",
                    receivedMessage.DeliveryTag,
                    ex.MessageId));

                acknowledgement.NegativeAcknowledge(receivedMessage.DeliveryTag, requeue: true);
            }
            catch (Exception ex)
            {
                logger.LogException(
                    SR.T("RabbitMQ listener. Ошибка обработки сообщения. DeliveryTag='{0}'.", receivedMessage.DeliveryTag),
                    ex);

                var requeue = RabbitMqDeliveryPolicy.ShouldRequeue(configuration, headers);
                acknowledgement.NegativeAcknowledge(receivedMessage.DeliveryTag, requeue);
                logger.Log(SR.T(
                    "RabbitMQ listener. Сообщение отклонено. DeliveryTag='{0}', Requeue='{1}'.",
                    receivedMessage.DeliveryTag,
                    requeue));
            }
        }
    }
}
