using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Exceptions;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using DomainConnection = IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection.IConnection;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections
{
    /// <summary>
    /// Подключение к RabbitMQ для request-reply (SentAndWait).
    /// </summary>
    internal sealed class RabbitMqRequestReplyConnection : DomainConnection, ILeaveOpenOnDispose
    {
        private readonly RabbitMqRequestReplyConfiguration configuration;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]>> pendingReplies = new();
        private RabbitMQ.Client.IConnection connection;
        private IModel publishChannel;
        private IModel consumeChannel;
        private AsyncEventingBasicConsumer consumer;
        private string replyAddress = string.Empty;
        private bool exclusiveReplyQueue;
        private bool disposed;

        public RabbitMqRequestReplyConnection(RabbitMqRequestReplyConfiguration configuration)
            : this(configuration, leaveOpenOnDispose: false, openConnection: true)
        {
        }

        internal RabbitMqRequestReplyConnection(
            RabbitMqRequestReplyConfiguration configuration,
            bool leaveOpenOnDispose,
            bool openConnection = true)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            LeaveOpenOnDispose = leaveOpenOnDispose;
            if (openConnection)
            {
                Open();
            }
        }

        public bool LeaveOpenOnDispose { get; }

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
            if (disposed || LeaveOpenOnDispose)
            {
                return;
            }

            disposed = true;
            DisposeInternal(deleteExclusiveQueue: true);
        }

        internal byte[] CompleteWaitingForResponse(string correlationId, TimeSpan timeout)
            => WaitForResponseAsync(correlationId, timeout, CancellationToken.None).GetAwaiter().GetResult();

        internal async Task<byte[]> WaitForResponseAsync(
            string correlationId,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                throw new ArgumentException("Correlation id is required.", nameof(correlationId));
            }

            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!pendingReplies.TryAdd(correlationId, tcs))
            {
                throw new InvalidOperationException("Duplicate correlation id.");
            }

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeout);

                using (timeoutCts.Token.Register(() =>
                {
                    if (pendingReplies.TryRemove(correlationId, out var pending))
                    {
                        pending.TrySetCanceled(timeoutCts.Token);
                    }
                }))
                {
                    try
                    {
                        return await tcs.Task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new RequestReplyTimeoutException(
                            $"RabbitMQ request-reply response was not received within {timeout.TotalSeconds:0} seconds.");
                    }
                }
            }
            finally
            {
                pendingReplies.TryRemove(correlationId, out _);
            }
        }

        internal void CancelPendingResponse(string correlationId)
        {
            if (pendingReplies.TryRemove(correlationId, out var pending))
            {
                pending.TrySetCanceled();
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

            consumer = new AsyncEventingBasicConsumer(consumeChannel);
            consumer.Received += OnReplyReceivedAsync;
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

        private Task OnReplyReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
        {
            var correlationId = eventArgs.BasicProperties?.CorrelationId;
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                return Task.CompletedTask;
            }

            if (pendingReplies.TryRemove(correlationId, out var pending))
            {
                pending.TrySetResult(eventArgs.Body.ToArray());
            }

            return Task.CompletedTask;
        }

        private void DisposeInternal(bool deleteExclusiveQueue)
        {
            if (consumer != null)
            {
                consumer.Received -= OnReplyReceivedAsync;
                consumer = null;
            }

            foreach (var key in pendingReplies.Keys.ToArray())
            {
                if (pendingReplies.TryRemove(key, out var pending))
                {
                    pending.TrySetCanceled();
                }
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
