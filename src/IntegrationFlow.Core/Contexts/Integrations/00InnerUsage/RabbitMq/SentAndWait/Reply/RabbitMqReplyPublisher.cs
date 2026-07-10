using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Exceptions;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply
{
    /// <summary>
    /// Публикация RPC-ответа на <see cref="RabbitMqReceivedMessage.ReplyTo"/>.
    /// </summary>
    public sealed class RabbitMqReplyPublisher
    {
        private readonly RabbitMqConnectionSettings connectionSettings;
        private readonly string contentType;
        private readonly RabbitMqRequestReplyConfiguration? pooledConfiguration;
        private readonly bool replyMandatory;

        /// <summary>
        /// Creates publisher from request-reply profile configuration.
        /// </summary>
        public RabbitMqReplyPublisher(RabbitMqRequestReplyConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            configuration.Validate();
            connectionSettings = configuration.ToConnectionSettings();
            contentType = configuration.ContentType;
            replyMandatory = configuration.ReplyMandatory;
            pooledConfiguration = configuration.ReuseReplyConnection ? configuration : null;
        }

        /// <summary>
        /// Publishes reply for the given request message.
        /// </summary>
        public void PublishReply(RabbitMqReceivedMessage request, object responseBody)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!request.IsRequestReply)
            {
                throw new InvalidOperationException("Request message does not contain ReplyTo address.");
            }

            PublishReply(request.ReplyTo, request.CorrelationId, responseBody);
        }

        /// <summary>
        /// Publishes reply to the specified reply address.
        /// </summary>
        public void PublishReply(string replyTo, string correlationId, object responseBody)
        {
            if (string.IsNullOrWhiteSpace(replyTo))
            {
                throw new ArgumentException("ReplyTo address is required.", nameof(replyTo));
            }

            var body = IntegrationPayloadSerializer.SerializeToBytes(responseBody);
            PublishReply(replyTo, correlationId, body);
        }

        /// <summary>
        /// Publishes raw reply body.
        /// </summary>
        public void PublishReply(string replyTo, string correlationId, byte[] responseBody)
        {
            if (string.IsNullOrWhiteSpace(replyTo))
            {
                throw new ArgumentException("ReplyTo address is required.", nameof(replyTo));
            }

            if (pooledConfiguration != null)
            {
                PublishReplyUsingPool(replyTo, correlationId, responseBody);
                return;
            }

            PublishReplyUsingDedicatedConnection(replyTo, correlationId, responseBody);
        }

        /// <summary>
        /// Publishes UTF-8 text reply.
        /// </summary>
        public void PublishTextReply(RabbitMqReceivedMessage request, string responseText)
        {
            PublishReply(request, responseText ?? string.Empty);
        }

        private void PublishReplyUsingPool(string replyTo, string correlationId, byte[] responseBody)
        {
            try
            {
                var pooledChannel = RabbitMqReplyPublisherPool.GetOrAdd(pooledConfiguration!);
                PublishReply(pooledChannel.Channel, replyTo, correlationId, responseBody);
            }
            catch
            {
                RabbitMqReplyPublisherPool.Invalidate(pooledConfiguration!);
                throw;
            }
        }

        private void PublishReplyUsingDedicatedConnection(string replyTo, string correlationId, byte[] responseBody)
        {
            var factory = RabbitMqConnectionFactory.Create(connectionSettings);
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            PublishReply(channel, replyTo, correlationId, responseBody, waitForReturn: replyMandatory);
        }

        private void PublishReply(IModel channel, string replyTo, string correlationId, byte[] responseBody)
            => PublishReply(channel, replyTo, correlationId, responseBody, waitForReturn: replyMandatory);

        private void PublishReply(
            IModel channel,
            string replyTo,
            string correlationId,
            byte[] responseBody,
            bool waitForReturn)
        {
            using (RabbitMqDistributedTracing.StartProducerActivity(
                       "reply",
                       pooledConfiguration?.Name ?? "rpc_reply",
                       replyTo,
                       correlationId: correlationId))
            {
                var properties = channel.CreateBasicProperties();
                properties.ContentType = contentType;
                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    properties.CorrelationId = correlationId;
                }

                RabbitMqTracePropagation.Inject(properties);

                using var returnSignal = waitForReturn ? new ManualResetEventSlim(false) : null;
                EventHandler<BasicReturnEventArgs>? onReturn = null;
                if (waitForReturn)
                {
                    onReturn = (_, _) => returnSignal!.Set();
                    channel.BasicReturn += onReturn;
                }

                try
                {
                    channel.BasicPublish(
                        exchange: string.Empty,
                        routingKey: replyTo,
                        mandatory: waitForReturn,
                        basicProperties: properties,
                        body: responseBody ?? Array.Empty<byte>());

                    if (waitForReturn)
                    {
                        returnSignal!.Wait(TimeSpan.FromMilliseconds(100));
                        if (returnSignal.IsSet)
                        {
                            throw new UnroutableMessageException("RabbitMQ returned BasicReturn for mandatory RPC reply.");
                        }
                    }
                }
                finally
                {
                    if (onReturn != null)
                    {
                        channel.BasicReturn -= onReturn;
                    }
                }
            }
        }
    }
}
