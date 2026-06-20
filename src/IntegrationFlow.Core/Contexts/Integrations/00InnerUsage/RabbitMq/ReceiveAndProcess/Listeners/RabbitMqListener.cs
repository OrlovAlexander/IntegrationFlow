using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners
{
    /// <summary>
    /// Асинхронный потокобезопасный слушатель очереди RabbitMQ с ручным подтверждением получения сообщений.
    /// </summary>
    internal sealed class RabbitMqListener : ListenerBase
    {
        private readonly object channelSync = new();
        private IConnection connection;
        private IModel channel;
        private CancellationTokenSource cancellationTokenSource;
        private volatile bool listening;

        /// <inheritdoc />
        protected override void Listen(object configuration)
        {
            var rabbitMqConfiguration = (RabbitMqConfiguration)configuration;

            if (string.IsNullOrWhiteSpace(rabbitMqConfiguration.QueueName))
            {
                throw new InvalidOperationException(SR.T("Не задано имя очереди RabbitMQ."));
            }

            cancellationTokenSource = new CancellationTokenSource();

            try
            {
                ListenAsync(rabbitMqConfiguration, cancellationTokenSource.Token).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.LogException(SR.T("RabbitMQ listener. Ошибка прослушивания очереди '{0}'.", rabbitMqConfiguration.QueueName), ex);
            }
            finally
            {
                listening = false;
            }
        }

        /// <inheritdoc />
        protected override void DisposeInternal(bool disposing)
        {
            cancellationTokenSource?.Cancel();

            lock (channelSync)
            {
                CloseChannelAndConnection();
                listening = false;
            }
        }

        /// <inheritdoc />
        protected override ListenerStatuses GetStatusInternal(ListenerStatuses listenerStatuses)
        {
            lock (channelSync)
            {
                if (listening && connection != null && connection.IsOpen && channel != null && channel.IsOpen)
                {
                    return ListenerStatuses.Started;
                }
            }

            return ListenerStatuses.NotStarted;
        }

        private async Task ListenAsync(RabbitMqConfiguration configuration, CancellationToken cancellationToken)
        {
            var factory = CreateConnectionFactory(configuration);
            connection = factory.CreateConnection();
            channel = connection.CreateModel();

            channel.BasicQos(prefetchSize: 0, prefetchCount: configuration.PrefetchCount, global: false);
            channel.QueueDeclarePassive(configuration.QueueName);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (_, eventArgs) =>
            {
                await HandleReceivedAsync(eventArgs, cancellationToken).ConfigureAwait(false);
            };

            channel.BasicConsume(
                queue: configuration.QueueName,
                autoAck: false,
                consumer: consumer);

            listening = true;
            Logger.Log(SR.T("RabbitMQ listener. Подключение к очереди '{0}' установлено.", configuration.QueueName));

            try
            {
                while (!cancellationToken.IsCancellationRequested && connection.IsOpen)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task HandleReceivedAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var receivedMessage = new RabbitMqReceivedMessage(
                eventArgs.Body.ToArray(),
                eventArgs.DeliveryTag,
                eventArgs.RoutingKey);

            try
            {
                ProcessMessage(receivedMessage);
                Acknowledge(receivedMessage.DeliveryTag);
                Logger.Log(SR.T("RabbitMQ listener. Сообщение подтверждено. DeliveryTag='{0}'.", receivedMessage.DeliveryTag));
            }
            catch (Exception ex)
            {
                Logger.LogException(
                    SR.T("RabbitMQ listener. Ошибка обработки сообщения. DeliveryTag='{0}'.", receivedMessage.DeliveryTag),
                    ex);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private void Acknowledge(ulong deliveryTag)
        {
            lock (channelSync)
            {
                if (channel == null || !channel.IsOpen)
                {
                    return;
                }

                channel.BasicAck(deliveryTag, multiple: false);
            }
        }

        private static ConnectionFactory CreateConnectionFactory(RabbitMqConfiguration configuration)
        {
            return new ConnectionFactory
            {
                HostName = configuration.HostName,
                Port = configuration.Port,
                UserName = configuration.UserName,
                Password = configuration.Password,
                VirtualHost = configuration.VirtualHost,
                AutomaticRecoveryEnabled = configuration.AutomaticRecoveryEnabled,
                DispatchConsumersAsync = true,
                ClientProvidedName = configuration.ClientProvidedName
            };
        }

        private void CloseChannelAndConnection()
        {
            try
            {
                channel?.Close();
            }
            catch (Exception ex)
            {
                Logger.LogException(SR.T("RabbitMQ listener. Ошибка закрытия канала."), ex);
            }
            finally
            {
                channel?.Dispose();
                channel = null;
            }

            try
            {
                connection?.Close();
            }
            catch (Exception ex)
            {
                Logger.LogException(SR.T("RabbitMQ listener. Ошибка закрытия соединения."), ex);
            }
            finally
            {
                connection?.Dispose();
                connection = null;
            }
        }
    }
}
