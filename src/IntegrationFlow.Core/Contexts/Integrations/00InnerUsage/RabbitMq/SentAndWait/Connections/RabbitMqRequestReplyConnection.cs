using System;
using System.Threading;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using DomainConnection = IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection.IConnection;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections
{
    /// <summary>
    /// Подключение к RabbitMQ для request-reply (SentAndWait).
    /// </summary>
    internal sealed class RabbitMqRequestReplyConnection : DomainConnection
    {
        private readonly RabbitMqRequestReplyConfiguration configuration;
        private readonly object pendingSync = new();
        private RabbitMQ.Client.IConnection connection;
        private IModel publishChannel;
        private IModel consumeChannel;
        private EventingBasicConsumer consumer;
        private string replyAddress = string.Empty;
        private bool exclusiveReplyQueue;
        private bool disposed;
        private string pendingCorrelationId = string.Empty;
        private ManualResetEventSlim pendingSignal;
        private byte[] pendingResponse;

        public RabbitMqRequestReplyConnection(RabbitMqRequestReplyConfiguration configuration)
            : this(configuration, openConnection: true)
        {
        }

        internal RabbitMqRequestReplyConnection(RabbitMqRequestReplyConfiguration configuration, bool openConnection)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (openConnection)
            {
                Open();
            }
        }

        internal IModel PublishChannel => publishChannel;

        internal string ReplyAddress => replyAddress;

        public bool NeedReconnect()
        {
            return connection == null ||
                   !connection.IsOpen ||
                   publishChannel == null ||
                   !publishChannel.IsOpen ||
                   consumeChannel == null ||
                   !consumeChannel.IsOpen;
        }

        public bool Reconnect()
        {
            DisposeInternal(deleteExclusiveQueue: false);
            Open();
            return !NeedReconnect();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeInternal(deleteExclusiveQueue: true);
        }

        internal void BeginWaitingForResponse(string correlationId)
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                throw new ArgumentException("Correlation id is required.", nameof(correlationId));
            }

            lock (pendingSync)
            {
                pendingCorrelationId = correlationId;
                pendingResponse = null;
                pendingSignal?.Dispose();
                pendingSignal = new ManualResetEventSlim(false);
            }
        }

        internal byte[] CompleteWaitingForResponse(TimeSpan timeout)
        {
            ManualResetEventSlim signal;
            lock (pendingSync)
            {
                signal = pendingSignal ?? throw new InvalidOperationException("Response wait was not started.");
            }

            try
            {
                if (!signal.Wait(timeout))
                {
                    throw new Exceptions.RequestReplyTimeoutException(
                        $"RabbitMQ request-reply response was not received within {timeout.TotalSeconds:0} seconds.");
                }

                lock (pendingSync)
                {
                    return pendingResponse ?? Array.Empty<byte>();
                }
            }
            finally
            {
                lock (pendingSync)
                {
                    pendingCorrelationId = string.Empty;
                    pendingResponse = null;
                    pendingSignal?.Dispose();
                    pendingSignal = null;
                }
            }
        }

        internal void CancelWaitingForResponse()
        {
            lock (pendingSync)
            {
                pendingCorrelationId = string.Empty;
                pendingResponse = null;
                pendingSignal?.Dispose();
                pendingSignal = null;
            }
        }

        private void Open()
        {
            configuration.Validate();

            var factory = RabbitMqConnectionFactory.Create(configuration.ToConnectionSettings());
            connection = factory.CreateConnection();
            publishChannel = connection.CreateModel();
            consumeChannel = connection.CreateModel();

            replyAddress = configuration.ReplyMode switch
            {
                RabbitMqReplyMode.DirectReplyTo => RabbitMqRequestReplyConstants.DirectReplyToAddress,
                RabbitMqReplyMode.ExclusiveQueue => DeclareExclusiveReplyQueue(),
                _ => throw new InvalidOperationException($"Unsupported reply mode: {configuration.ReplyMode}.")
            };

            consumer = new EventingBasicConsumer(consumeChannel);
            consumer.Received += OnReplyReceived;
            consumeChannel.BasicConsume(
                queue: replyAddress,
                autoAck: true,
                consumer: consumer);
        }

        private string DeclareExclusiveReplyQueue()
        {
            exclusiveReplyQueue = true;
            var declare = consumeChannel.QueueDeclare(
                queue: string.Empty,
                durable: false,
                exclusive: true,
                autoDelete: true,
                arguments: null);

            return declare.QueueName;
        }

        private void OnReplyReceived(object sender, BasicDeliverEventArgs eventArgs)
        {
            var correlationId = eventArgs.BasicProperties?.CorrelationId;
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                return;
            }

            lock (pendingSync)
            {
                if (!string.Equals(pendingCorrelationId, correlationId, StringComparison.Ordinal))
                {
                    return;
                }

                pendingResponse = eventArgs.Body.ToArray();
                pendingSignal?.Set();
            }
        }

        private void DisposeInternal(bool deleteExclusiveQueue)
        {
            if (consumer != null)
            {
                consumer.Received -= OnReplyReceived;
                consumer = null;
            }

            lock (pendingSync)
            {
                pendingSignal?.Dispose();
                pendingSignal = null;
                pendingCorrelationId = string.Empty;
                pendingResponse = null;
            }

            TryCloseChannel(consumeChannel);
            consumeChannel = null;

            if (deleteExclusiveQueue &&
                exclusiveReplyQueue &&
                !string.IsNullOrWhiteSpace(replyAddress) &&
                publishChannel != null &&
                publishChannel.IsOpen)
            {
                try
                {
                    publishChannel.QueueDelete(replyAddress);
                }
                catch
                {
                }
            }

            TryCloseChannel(publishChannel);
            publishChannel = null;

            try
            {
                connection?.Close();
            }
            catch
            {
            }
            finally
            {
                connection?.Dispose();
                connection = null;
            }

            replyAddress = string.Empty;
            exclusiveReplyQueue = false;
        }

        private static void TryCloseChannel(IModel channel)
        {
            if (channel == null)
            {
                return;
            }

            try
            {
                channel.Close();
            }
            catch
            {
            }
            finally
            {
                channel.Dispose();
            }
        }
    }
}
