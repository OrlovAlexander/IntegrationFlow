using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Exceptions;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Transmitters
{
    /// <summary>
    /// Request-reply transmitter для SentAndWait через RabbitMQ.
    /// </summary>
    internal sealed class RabbitMqRequestReplyTransmitter : ITransmitter, ITransmitterAsync, IMetricsAwareTransmitter
    {
        private readonly RabbitMqRequestReplyConfiguration configuration;
        private readonly RabbitMqRequestReplyConnection connection;
        private readonly SemaphoreSlim? concurrencyGate;

        public RabbitMqRequestReplyTransmitter(
            IConfiguration configuration,
            RabbitMqRequestReplyConnection connection)
        {
            this.configuration = (RabbitMqRequestReplyConfiguration)configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            concurrencyGate = CreateConcurrencyGate(this.configuration.MaxConcurrentRequests);
        }

        public IIntegrationFlowMetrics? Metrics { get; set; }

        public ObtainedData Transmit(TransmitData transmitData)
            => TransmitAsync(transmitData, CancellationToken.None).GetAwaiter().GetResult();

        public async Task<ObtainedData> TransmitAsync(TransmitData transmitData, CancellationToken cancellationToken)
        {
            if (concurrencyGate != null)
            {
                await concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            var stopwatch = Stopwatch.StartNew();
            var success = false;
            var timedOut = false;
            try
            {
                configuration.Validate();

                var maxAttempts = GetMaxAttempts(transmitData);
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    if (attempt > 0)
                    {
                        Metrics?.RecordRequestReplyRetryAfterTimeout(configuration.Name);
                        await Task.Delay(SentAndWaitIntegrationOptions.RetryDelay, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    try
                    {
                        var result = await TransmitOnceAsync(transmitData, cancellationToken).ConfigureAwait(false);
                        success = !result.IsFailed;
                        return result;
                    }
                    catch (SentAndWaitTimeoutException) when (attempt < maxAttempts - 1)
                    {
                        continue;
                    }
                }

                throw new InvalidOperationException("Request-reply transmit did not complete.");
            }
            catch (SentAndWaitTimeoutException ex)
            {
                timedOut = true;
                throw;
            }
            finally
            {
                Metrics?.RecordRequestReply(
                    configuration.Name,
                    stopwatch.Elapsed,
                    success,
                    timedOut);
                concurrencyGate?.Release();
            }
        }

        private async Task<ObtainedData> TransmitOnceAsync(TransmitData transmitData, CancellationToken cancellationToken)
        {
            if (connection.NeedReconnect() && !connection.Reconnect())
            {
                return new ObtainedData(null, isFailed: true);
            }

            var correlationId = Guid.NewGuid().ToString("N");
            var waitTask = connection.WaitForResponseAsync(
                correlationId,
                configuration.GetResponseTimeout(),
                cancellationToken);

            try
            {
                PublishRequest(transmitData, correlationId);
                var responseBody = await waitTask.ConfigureAwait(false);
                return CreateObtainedData(responseBody);
            }
            catch (RequestReplyTimeoutException ex)
            {
                connection.CancelPendingResponse(correlationId);
                throw new SentAndWaitTimeoutException(ex.Message, ex);
            }
            catch
            {
                connection.CancelPendingResponse(correlationId);
                throw;
            }
        }

        private static int GetMaxAttempts(TransmitData transmitData)
        {
            if (!SentAndWaitIntegrationOptions.RetryOnTimeout ||
                string.IsNullOrWhiteSpace(transmitData.MessageId) ||
                SentAndWaitIntegrationOptions.MaxRetries <= 0)
            {
                return 1;
            }

            return 1 + SentAndWaitIntegrationOptions.MaxRetries;
        }

        private void PublishRequest(TransmitData transmitData, string correlationId)
        {
            var channel = connection.PublishChannel;
            if (configuration.ValidateTopology)
            {
                ValidateTopologyPassive(channel);
            }

            var messageId = string.IsNullOrWhiteSpace(transmitData.MessageId)
                ? Guid.NewGuid().ToString("N")
                : transmitData.MessageId;
            var body = RabbitMqPublishTransmitter.SerializeBody(transmitData.Data);

            var properties = channel.CreateBasicProperties();
            properties.ContentType = configuration.ContentType;
            properties.DeliveryMode = configuration.Persistent ? (byte)2 : (byte)1;
            properties.CorrelationId = correlationId;
            properties.ReplyTo = connection.ReplyAddress;
            properties.MessageId = messageId;

            channel.BasicPublish(
                exchange: configuration.GetRequestExchange(),
                routingKey: configuration.GetRequestRoutingKey(),
                mandatory: configuration.Mandatory,
                basicProperties: properties,
                body: body);
        }

        private static SemaphoreSlim? CreateConcurrencyGate(int maxConcurrentRequests)
        {
            if (maxConcurrentRequests <= 0)
            {
                return null;
            }

            return new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        }

        private static ObtainedData CreateObtainedData(byte[] responseBody)
        {
            if (responseBody == null || responseBody.Length == 0)
            {
                return new ObtainedData(null, isFailed: true);
            }

            return new ObtainedData(Encoding.UTF8.GetString(responseBody));
        }

        private void ValidateTopologyPassive(IModel channel)
        {
            if (configuration.RequestTarget == RabbitMqRequestReplyTarget.Queue)
            {
                channel.QueueDeclarePassive(configuration.QueueName);
                return;
            }

            channel.ExchangeDeclarePassive(configuration.Exchange);
        }
    }
}
