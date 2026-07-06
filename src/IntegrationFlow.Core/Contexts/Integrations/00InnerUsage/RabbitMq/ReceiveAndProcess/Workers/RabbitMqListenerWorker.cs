using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Logging;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Workers;

/// <summary>
/// Async worker for RabbitMQ consume loop (shared by listener and hosted service).
/// </summary>
internal sealed class RabbitMqListenerWorker
{
    private const int ShutdownDrainTimeoutSeconds = 30;

    private readonly object channelSync = new();

    public async Task RunAsync(
        RabbitMqConfiguration configuration,
        Func<object, Task> processMessageAsync,
        IIntegrationLogger logger,
        CancellationToken cancellationToken,
        Action? onStarted = null,
        Action? onStopped = null,
        IIntegrationFlowMetrics? metrics = null,
        RabbitMqTransportHealthRegistry? healthRegistry = null)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (processMessageAsync == null)
        {
            throw new ArgumentNullException(nameof(processMessageAsync));
        }

        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        if (string.IsNullOrWhiteSpace(configuration.QueueName))
        {
            throw new InvalidOperationException(SR.T("Не задано имя очереди RabbitMQ."));
        }

        var inFlightTracker = new RabbitMqListenerInFlightTracker();
        var consumerStopping = false;
        var startedInvoked = false;
        var reconnectAttempt = 0;
        var profileName = GetProfileName(configuration);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                IConnection? connection = null;
                IModel? channel = null;
                string? consumerTag = null;

                try
                {
                    var factory = RabbitMqConnectionFactory.Create(
                        RabbitMqPublishConfiguration.ToConnectionSettings(configuration));

                    lock (channelSync)
                    {
                        connection = factory.CreateConnection();
                        channel = connection.CreateModel();
                    }

                    var acknowledgement = new RabbitMqChannelAcknowledgement(
                        channelSync,
                        () => channel!,
                        logger);

                    var messageHandler = new RabbitMqReceivedMessageHandler(
                        processMessageAsync,
                        acknowledgement,
                        logger,
                        inFlightTracker,
                        () => consumerStopping || cancellationToken.IsCancellationRequested,
                        metrics,
                        profileName);

                    lock (channelSync)
                    {
                        channel!.BasicQos(prefetchSize: 0, prefetchCount: configuration.PrefetchCount, global: false);
                        channel.QueueDeclarePassive(configuration.QueueName);

                        var consumer = new AsyncEventingBasicConsumer(channel);
                        consumer.Received += async (_, eventArgs) =>
                        {
                            var receivedMessage = new RabbitMqReceivedMessage(
                                eventArgs.Body.ToArray(),
                                eventArgs.DeliveryTag,
                                eventArgs.RoutingKey,
                                eventArgs.BasicProperties?.MessageId,
                                eventArgs.BasicProperties?.CorrelationId,
                                eventArgs.BasicProperties?.ReplyTo);

                            await messageHandler.HandleAsync(
                                    receivedMessage,
                                    configuration,
                                    eventArgs.BasicProperties?.Headers,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        };

                        consumerTag = channel.BasicConsume(
                            queue: configuration.QueueName,
                            autoAck: false,
                            consumer: consumer);
                    }

                    if (!startedInvoked)
                    {
                        using (RabbitMqStructuredLogging.BeginTransportScope(
                                   logger,
                                   profileName,
                                   RabbitMqTransportLogKind.Listener))
                        {
                            logger.Log(SR.T(
                                "RabbitMQ listener. Подключение к очереди '{0}' установлено.",
                                configuration.QueueName));
                        }

                        onStarted?.Invoke();
                        startedInvoked = true;
                    }
                    else
                    {
                        using (RabbitMqStructuredLogging.BeginTransportScope(
                                   logger,
                                   profileName,
                                   RabbitMqTransportLogKind.Listener))
                        {
                            logger.Log(SR.T(
                                "RabbitMQ listener. Переподключение к очереди '{0}' выполнено.",
                                configuration.QueueName));
                        }
                    }

                    reconnectAttempt = 0;
                    healthRegistry?.ReportConnected(RabbitMqTransportKind.Listener, profileName);

                    var sessionEndedByCancellation = await RabbitMqConsumerSessionLifecycle
                        .WaitForSessionEndAsync(connection, cancellationToken)
                        .ConfigureAwait(false);

                    if (sessionEndedByCancellation)
                    {
                        break;
                    }

                    reconnectAttempt++;
                    metrics?.RecordListenerReconnect(profileName);
                    healthRegistry?.ReportReconnecting(
                        RabbitMqTransportKind.Listener,
                        profileName,
                        reconnectAttempt);
                    using (RabbitMqStructuredLogging.BeginTransportScope(
                               logger,
                               profileName,
                               RabbitMqTransportLogKind.Listener))
                    {
                        logger.Log(SR.T(
                            "RabbitMQ listener. Соединение с очередью '{0}' разорвано. Повторное подключение через {1} с.",
                            configuration.QueueName,
                            RabbitMqConsumerSessionLifecycle.GetReconnectDelaySeconds(reconnectAttempt)));
                    }
                    await RabbitMqConsumerSessionLifecycle
                        .DelayReconnectAsync(reconnectAttempt, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    reconnectAttempt++;
                    metrics?.RecordListenerReconnect(profileName);
                    healthRegistry?.ReportReconnecting(
                        RabbitMqTransportKind.Listener,
                        profileName,
                        reconnectAttempt,
                        ex.Message);
                    using (RabbitMqStructuredLogging.BeginTransportScope(
                               logger,
                               profileName,
                               RabbitMqTransportLogKind.Listener))
                    {
                        logger.LogException(
                            SR.T("RabbitMQ listener. Ошибка прослушивания очереди '{0}'.", configuration.QueueName),
                            ex);
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await RabbitMqConsumerSessionLifecycle
                        .DelayReconnectAsync(reconnectAttempt, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    consumerStopping = true;

                    lock (channelSync)
                    {
                        if (!string.IsNullOrEmpty(consumerTag) && channel != null && channel.IsOpen)
                        {
                            try
                            {
                                channel.BasicCancel(consumerTag);
                            }
                            catch (Exception ex)
                            {
                                logger.LogException(
                                    SR.T("RabbitMQ listener. Ошибка отмены consumer."),
                                    ex);
                            }
                        }
                    }

                    try
                    {
                        await inFlightTracker
                            .WaitForZeroAsync(TimeSpan.FromSeconds(ShutdownDrainTimeoutSeconds), cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                    }

                    lock (channelSync)
                    {
                        CloseChannelAndConnection(channel, connection, logger);
                    }

                    healthRegistry?.ReportDisconnected(RabbitMqTransportKind.Listener, profileName);
                    consumerStopping = false;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            healthRegistry?.ReportStopped(RabbitMqTransportKind.Listener, profileName);
            onStopped?.Invoke();
        }
    }

    private static string GetProfileName(RabbitMqConfiguration configuration)
        => string.IsNullOrWhiteSpace(configuration.Name) ? configuration.QueueName : configuration.Name;

    private static void CloseChannelAndConnection(
        IModel? channel,
        IConnection? connection,
        IIntegrationLogger logger)
    {
        try
        {
            channel?.Close();
        }
        catch (Exception ex)
        {
            logger.LogException(SR.T("RabbitMQ listener. Ошибка закрытия канала."), ex);
        }
        finally
        {
            channel?.Dispose();
        }

        try
        {
            connection?.Close();
        }
        catch (Exception ex)
        {
            logger.LogException(SR.T("RabbitMQ listener. Ошибка закрытия соединения."), ex);
        }
        finally
        {
            connection?.Dispose();
        }
    }
}
