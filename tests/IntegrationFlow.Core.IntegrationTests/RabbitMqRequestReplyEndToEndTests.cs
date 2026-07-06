using System.Linq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Transmitters;
using IntegrationFlow.Contexts.Integrations._00Samples.SentAndWait.ResponseCache;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using IntegrationFlow.IntegrationTests.Infrastructure;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RabbitMqRequestReplyEndToEndTests : IAsyncLifetime
{
    private const string QueueName = "integration.rpc.request-reply";

    private readonly RabbitMqContainerFixture rabbitMq = new();

    public Task InitializeAsync() => rabbitMq.InitializeAsync();

    public Task DisposeAsync() => rabbitMq.DisposeAsync();

    [Fact]
    public void Transmit_ReturnsResponseFromServer()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        DeclareRequestQueue();

        var serverTask = Task.Run(ServeOneRequestAndReply);

        var configuration = CreateRuntimeConfiguration(responseTimeoutSeconds: 10);
        using var connection = new RabbitMqRequestReplyConnection(configuration);
        var transmitter = new RabbitMqRequestReplyTransmitter(configuration, connection);

        var result = transmitter.Transmit(new TransmitData("""{"orderId":42}"""));

        serverTask.GetAwaiter().GetResult();

        Assert.False(result.IsFailed);
        Assert.Equal("""{"status":"ok","orderId":42}""", result.Data);
    }

    [Fact]
    public void Transmit_ThrowsTimeoutWhenServerDoesNotReply()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        DeclareRequestQueue();

        var configuration = CreateRuntimeConfiguration(responseTimeoutSeconds: 1);
        using var connection = new RabbitMqRequestReplyConnection(configuration);
        var transmitter = new RabbitMqRequestReplyTransmitter(configuration, connection);

        Assert.Throws<SentAndWaitTimeoutException>(
            () => transmitter.Transmit(new TransmitData("timeout-request")));
    }

    [Fact]
    public async Task TransmitAsync_ReturnsResponseFromServer()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        DeclareRequestQueue();

        var serverTask = Task.Run(ServeOneRequestAndReply);

        var configuration = CreateRuntimeConfiguration(responseTimeoutSeconds: 10);
        using var connection = new RabbitMqRequestReplyConnection(configuration);
        var transmitter = new RabbitMqRequestReplyTransmitter(configuration, connection);

        var result = await transmitter.TransmitAsync(new TransmitData("""{"orderId":42}"""), CancellationToken.None);

        await serverTask;

        Assert.False(result.IsFailed);
        Assert.Equal("""{"status":"ok","orderId":42}""", result.Data);
    }

    [Fact]
    public async Task TransmitAsync_ThrowsTimeoutWhenServerDoesNotReply()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        DeclareRequestQueue();

        var configuration = CreateRuntimeConfiguration(responseTimeoutSeconds: 1);
        using var connection = new RabbitMqRequestReplyConnection(configuration);
        var transmitter = new RabbitMqRequestReplyTransmitter(configuration, connection);

        await Assert.ThrowsAsync<SentAndWaitTimeoutException>(
            () => transmitter.TransmitAsync(new TransmitData("timeout-request"), CancellationToken.None));
    }

    [Fact]
    public async Task TransmitAsync_SupportsParallelRequestsWhenConfigured()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        DeclareRequestQueue();

        var configuration = CreateRuntimeConfiguration(responseTimeoutSeconds: 10);
        configuration.MaxConcurrentRequests = 4;

        using var connection = new RabbitMqRequestReplyConnection(configuration);
        var transmitter = new RabbitMqRequestReplyTransmitter(configuration, connection);

        var serverTask = Task.Run(() => ServeParallelRequests(count: 4));

        var tasks = Enumerable.Range(1, 4)
            .Select(orderId => transmitter.TransmitAsync(
                new TransmitData($"{{\"orderId\":{orderId}}}"),
                CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        await serverTask;

        Assert.All(results, result => Assert.False(result.IsFailed));
        Assert.Contains(results, result => result.Data?.ToString()?.Contains("orderId") == true);
    }

    [Fact]
    public async Task TransmitAsync_WithMessageId_ReturnsCachedResponseOnSecondCall()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        DeclareRequestQueue();

        var responseStore = new InMemoryRequestReplyResponseStore();
        var messageId = Guid.NewGuid().ToString("N");
        var configuration = CreateRuntimeConfiguration(responseTimeoutSeconds: 10);

        using var connection = new RabbitMqRequestReplyConnection(configuration);
        var transmitter = new RabbitMqRequestReplyTransmitter(configuration, connection);

        var serverFirst = Task.Run(() =>
            ServeOneCachedRequestAsync(responseStore, configuration, TimeSpan.Zero));
        var first = await transmitter.TransmitAsync(new TransmitData("""{"n":1}""", messageId), CancellationToken.None);
        await serverFirst;

        var serverSecond = Task.Run(() =>
            ServeOneCachedRequestAsync(responseStore, configuration, TimeSpan.Zero));
        var second = await transmitter.TransmitAsync(new TransmitData("""{"n":2}""", messageId), CancellationToken.None);
        await serverSecond;

        Assert.False(first.IsFailed);
        Assert.False(second.IsFailed);
        Assert.Equal(first.Data, second.Data);
    }

    [Fact]
    public async Task TransmitAsync_RetriesAfterTimeout_WhenMessageIdAndRetryEnabled()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        DeclareRequestQueue();

        var previousRetry = SentAndWaitIntegrationOptions.RetryOnTimeout;
        var previousMaxRetries = SentAndWaitIntegrationOptions.MaxRetries;
        var previousDelay = SentAndWaitIntegrationOptions.RetryDelay;
        try
        {
            SentAndWaitIntegrationOptions.RetryOnTimeout = true;
            SentAndWaitIntegrationOptions.MaxRetries = 1;
            SentAndWaitIntegrationOptions.RetryDelay = TimeSpan.FromMilliseconds(100);

            var responseStore = new InMemoryRequestReplyResponseStore();
            var messageId = Guid.NewGuid().ToString("N");
            var configuration = CreateRuntimeConfiguration(responseTimeoutSeconds: 1);
            var metrics = new RequestReplyMetricsSpy();

            var serverTask = Task.Run(() =>
                ServeOneCachedRequestAsync(
                    responseStore,
                    configuration,
                    delayBeforeReply: TimeSpan.FromMilliseconds(1800)));

            using var connection = new RabbitMqRequestReplyConnection(configuration);
            var transmitter = new RabbitMqRequestReplyTransmitter(configuration, connection)
            {
                Metrics = metrics
            };

            var result = await transmitter.TransmitAsync(
                new TransmitData("retry-request", messageId),
                CancellationToken.None);

            await serverTask;

            Assert.False(result.IsFailed);
            Assert.Equal(1, metrics.RetryCount);
        }
        finally
        {
            SentAndWaitIntegrationOptions.RetryOnTimeout = previousRetry;
            SentAndWaitIntegrationOptions.MaxRetries = previousMaxRetries;
            SentAndWaitIntegrationOptions.RetryDelay = previousDelay;
        }
    }

    private async Task ServeOneCachedRequestAsync(
        InMemoryRequestReplyResponseStore responseStore,
        RabbitMqRequestReplyConfiguration configuration,
        TimeSpan delayBeforeReply)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();

        BasicGetResult? delivery = null;
        for (var attempt = 0; attempt < 200 && delivery == null; attempt++)
        {
            delivery = channel.BasicGet(QueueName, autoAck: false);
            if (delivery == null)
            {
                await Task.Delay(50);
            }
        }

        Assert.NotNull(delivery);

        var request = new RabbitMqReceivedMessage(
            delivery!.Body.ToArray(),
            delivery.DeliveryTag,
            delivery.RoutingKey,
            delivery.BasicProperties?.MessageId,
            delivery.BasicProperties?.CorrelationId,
            delivery.BasicProperties?.ReplyTo);

        var replyPublisher = new RabbitMqReplyPublisher(configuration);
        await RabbitMqRpcServerPipeline.HandleAsync(
            request,
            replyPublisher,
            async _ =>
            {
                if (delayBeforeReply > TimeSpan.Zero)
                {
                    await Task.Delay(delayBeforeReply);
                }

                return """{"status":"ok","cached":true}""";
            },
            responseStore);

        channel.BasicAck(delivery.DeliveryTag, multiple: false);
    }

    private sealed class RequestReplyMetricsSpy : IIntegrationFlowMetrics
    {
        public int RetryCount { get; private set; }

        public void RecordMessageProcessed(string profileName, TimeSpan duration, bool success)
        {
        }

        public void RecordOutboxRelayPublished(int count)
        {
        }

        public void RecordOutboxRelayFailed(int count)
        {
        }

        public void RecordOutboxRelayAbandoned(int count)
        {
        }

        public void RecordOutboxPending(int count)
        {
        }

        public void RecordRequestReply(string profileName, TimeSpan duration, bool success, bool timedOut = false)
        {
        }

        public void RecordRequestReplyRetryAfterTimeout(string profileName) => RetryCount++;

        public void RecordRpcPendingRelayPublished(int count)
        {
        }

        public void RecordRpcPendingRelayFailed(int count)
        {
        }

        public void RecordRpcPendingRelayAbandoned(int count)
        {
        }

        public void RecordRpcPendingAwaiting(int count)
        {
        }

        public void RecordRpcPendingCompleted(string profileName, TimeSpan duration, bool success, bool timedOut = false)
        {
        }

        public void RecordListenerReconnect(string profileName)
        {
        }

        public void RecordListenerShutdownRequeue(string profileName)
        {
        }

        public void RecordConnectionPoolSize(string kind, int size)
        {
        }

        public void RecordBrokerConnected(string profileName, string kind, bool connected)
        {
        }
    }

    private void ServeOneRequestAndReply()
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();

        BasicGetResult? delivery = null;
        for (var attempt = 0; attempt < 100 && delivery == null; attempt++)
        {
            delivery = channel.BasicGet(QueueName, autoAck: false);
            if (delivery == null)
            {
                Thread.Sleep(50);
            }
        }

        Assert.NotNull(delivery);

        var request = new RabbitMqReceivedMessage(
            delivery!.Body.ToArray(),
            delivery.DeliveryTag,
            delivery.RoutingKey,
            delivery.BasicProperties?.MessageId,
            delivery.BasicProperties?.CorrelationId,
            delivery.BasicProperties?.ReplyTo);

        Assert.True(request.IsRequestReply);

        var replyPublisher = new RabbitMqReplyPublisher(CreateRuntimeConfiguration(responseTimeoutSeconds: 10));
        replyPublisher.PublishTextReply(request, """{"status":"ok","orderId":42}""");

        channel.BasicAck(delivery.DeliveryTag, multiple: false);
    }

    private void ServeParallelRequests(int count)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();

        for (var served = 0; served < count; served++)
        {
            BasicGetResult? delivery = null;
            for (var attempt = 0; attempt < 100 && delivery == null; attempt++)
            {
                delivery = channel.BasicGet(QueueName, autoAck: false);
                if (delivery == null)
                {
                    Thread.Sleep(50);
                }
            }

            Assert.NotNull(delivery);

            var request = new RabbitMqReceivedMessage(
                delivery!.Body.ToArray(),
                delivery.DeliveryTag,
                delivery.RoutingKey,
                delivery.BasicProperties?.MessageId,
                delivery.BasicProperties?.CorrelationId,
                delivery.BasicProperties?.ReplyTo);

            var replyPublisher = new RabbitMqReplyPublisher(CreateRuntimeConfiguration(responseTimeoutSeconds: 10));
            replyPublisher.PublishTextReply(request, """{"status":"ok"}""");

            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
    }

    private RabbitMqRequestReplyConfiguration CreateRuntimeConfiguration(int responseTimeoutSeconds)
    {
        var factory = rabbitMq.CreateConnectionFactory();
        return new RabbitMqRequestReplyConfiguration
        {
            Name = "OrdersRpc",
            HostName = factory.HostName,
            Port = factory.Port,
            UserName = factory.UserName,
            Password = factory.Password,
            VirtualHost = factory.VirtualHost,
            RequestTarget = RabbitMqRequestReplyTarget.Queue,
            QueueName = QueueName,
            ReplyMode = RabbitMqReplyMode.DirectReplyTo,
            ResponseTimeoutSeconds = responseTimeoutSeconds,
            ValidateTopology = true,
            ContentType = "application/json",
            ClientProvidedName = "IntegrationFlow.RequestReplyE2E"
        };
    }

    private void DeclareRequestQueue()
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(QueueName, durable: false, exclusive: false, autoDelete: true, arguments: null);
    }
}
