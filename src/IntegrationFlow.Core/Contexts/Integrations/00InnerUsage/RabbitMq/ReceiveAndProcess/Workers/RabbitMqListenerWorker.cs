using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Workers;

/// <summary>
/// Async worker for RabbitMQ consume loop (shared by listener and hosted service).
/// </summary>
internal sealed class RabbitMqListenerWorker
{
    private readonly object channelSync = new();

    public async Task RunAsync(
        RabbitMqConfiguration configuration,
        Func<object, Task> processMessageAsync,
        IIntegrationLogger logger,
        CancellationToken cancellationToken,
        Action? onStarted = null,
        Action? onStopped = null)
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

        IConnection? connection = null;
        IModel? channel = null;

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
                logger);

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
                        eventArgs.BasicProperties?.CorrelationId);

                    await messageHandler.HandleAsync(
                            receivedMessage,
                            configuration,
                            eventArgs.BasicProperties?.Headers,
                            cancellationToken)
                        .ConfigureAwait(false);
                };

                channel.BasicConsume(
                    queue: configuration.QueueName,
                    autoAck: false,
                    consumer: consumer);
            }

            logger.Log(SR.T(
                "RabbitMQ listener. Подключение к очереди '{0}' установлено.",
                configuration.QueueName));
            onStarted?.Invoke();

            await WaitForShutdownAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogException(
                SR.T("RabbitMQ listener. Ошибка прослушивания очереди '{0}'.", configuration.QueueName),
                ex);
            throw;
        }
        finally
        {
            lock (channelSync)
            {
                CloseChannelAndConnection(channel, connection, logger);
            }

            onStopped?.Invoke();
        }
    }

    private static async Task WaitForShutdownAsync(IConnection connection, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ShutdownEventArgs>? onShutdown = (_, _) => completion.TrySetResult(null!);

        connection.ConnectionShutdown += onShutdown;

        var registration = cancellationToken.Register(() => completion.TrySetResult(null));

        try
        {
            await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            connection.ConnectionShutdown -= onShutdown;
            registration.Dispose();
        }
    }

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
