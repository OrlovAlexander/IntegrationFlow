using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Logging;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Workers;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
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
        private readonly RabbitMqListenerInFlightTracker? inFlightTracker;
        private readonly Func<bool>? isConsumerStopping;
        private readonly IIntegrationFlowMetrics? metrics;
        private readonly string profileName;

        public RabbitMqReceivedMessageHandler(
            Func<object, Task> processMessageAsync,
            IRabbitMqMessageAcknowledgement acknowledgement,
            IIntegrationLogger logger,
            RabbitMqListenerInFlightTracker? inFlightTracker = null,
            Func<bool>? isConsumerStopping = null,
            IIntegrationFlowMetrics? metrics = null,
            string? profileName = null)
        {
            this.processMessageAsync = processMessageAsync ?? throw new ArgumentNullException(nameof(processMessageAsync));
            this.acknowledgement = acknowledgement ?? throw new ArgumentNullException(nameof(acknowledgement));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.inFlightTracker = inFlightTracker;
            this.isConsumerStopping = isConsumerStopping;
            this.metrics = metrics;
            this.profileName = profileName ?? string.Empty;
        }

        public async Task HandleAsync(
            RabbitMqReceivedMessage receivedMessage,
            RabbitMqConfiguration configuration,
            IDictionary<string, object> headers,
            CancellationToken cancellationToken)
        {
            using (RabbitMqStructuredLogging.BeginMessageScope(
                       logger,
                       profileName,
                       receivedMessage,
                       RabbitMqTransportLogKind.Listener))
            {
                using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "consume_started"))
                {
                    logger.Log(SR.T("RabbitMQ listener. Получено сообщение."));
                }

                if (ShouldStopAcceptingMessages(cancellationToken))
                {
                    NegativeAcknowledgeForShutdown(receivedMessage.DeliveryTag);
                    return;
                }

                using (RabbitMqDistributedTracing.StartConsumerActivity(
                           headers,
                           "receive",
                           profileName,
                           receivedMessage.MessageId,
                           receivedMessage.CorrelationId,
                           receivedMessage.DeliveryTag))
                {
                    inFlightTracker?.Increment();
                    try
                    {
                        await processMessageAsync(receivedMessage).ConfigureAwait(false);

                        if (ShouldStopAcceptingMessages(cancellationToken))
                        {
                            NegativeAcknowledgeForShutdown(receivedMessage.DeliveryTag);
                            return;
                        }

                        acknowledgement.Acknowledge(receivedMessage.DeliveryTag);
                        using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "ack"))
                        {
                            logger.Log(SR.T("RabbitMQ listener. Сообщение подтверждено."));
                        }
                    }
                    catch (MessageProcessingInProgressException ex)
                    {
                        using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "in_progress_requeue"))
                        {
                            logger.Log(SR.T(
                                "RabbitMQ listener. Сообщение обрабатывается параллельно. MessageId='{0}'.",
                                ex.MessageId));
                        }

                        acknowledgement.NegativeAcknowledge(receivedMessage.DeliveryTag, requeue: true);
                        metrics?.RecordConsumerOutcome(profileName, ConsumerOutcomeReason.InProgressRequeue);
                    }
                    catch (Exception ex)
                    {
                        logger.LogException(
                            SR.T("RabbitMQ listener. Ошибка обработки сообщения."),
                            ex);

                        var requeue = RabbitMqDeliveryPolicy.ShouldRequeue(configuration, headers);
                        acknowledgement.NegativeAcknowledge(receivedMessage.DeliveryTag, requeue);
                        metrics?.RecordConsumerOutcome(
                            profileName,
                            requeue ? ConsumerOutcomeReason.Requeue : ConsumerOutcomeReason.Nack);
                        using (IntegrationStructuredLogging.BeginOutcomeScope(
                                   logger,
                                   requeue ? "requeue" : "nack"))
                        {
                            logger.Log(SR.T(
                                "RabbitMQ listener. Сообщение отклонено. Requeue='{0}'.",
                                requeue));
                        }
                    }
                    finally
                    {
                        inFlightTracker?.Decrement();
                    }
                }
            }
        }

        private bool ShouldStopAcceptingMessages(CancellationToken cancellationToken)
            => cancellationToken.IsCancellationRequested || (isConsumerStopping?.Invoke() ?? false);

        private void NegativeAcknowledgeForShutdown(ulong deliveryTag)
        {
            acknowledgement.NegativeAcknowledge(deliveryTag, requeue: true);
            metrics?.RecordListenerShutdownRequeue(profileName);
            using (IntegrationStructuredLogging.BeginOutcomeScope(logger, "shutdown_requeue"))
            {
                logger.Log(SR.T("RabbitMQ listener. Сообщение возвращено в очередь при остановке."));
            }
        }
    }
}
