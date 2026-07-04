using System.Linq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Transmitters;
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
