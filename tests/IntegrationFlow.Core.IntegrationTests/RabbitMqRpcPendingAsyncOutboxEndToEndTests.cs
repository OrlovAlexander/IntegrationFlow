using System.Text;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply;
using IntegrationFlow.Contexts.Integrations._00Samples.RpcPending;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using IntegrationFlow.IntegrationTests.Infrastructure;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(RabbitMqIntegrationCollection.Name)]
public sealed class RabbitMqRpcPendingAsyncOutboxEndToEndTests : IAsyncLifetime
{
    private const string ProfileName = "OrdersRpcAsync";
    private const string RequestQueueName = "integration.rpc.async.request";
    private const string ResponseQueueName = "integration.rpc.async.response";

    private readonly RabbitMqContainerFixture rabbitMq = new();

    public Task InitializeAsync() => rabbitMq.InitializeAsync();

    public Task DisposeAsync() => rabbitMq.DisposeAsync();

    [Fact]
    public async Task RelayBatchAsync_CompletesPendingAfterResponseQueueDelivery()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        DeclareQueues();

        var store = new InMemoryRpcPendingStore();
        var pendingId = Guid.NewGuid();
        var requestPayload = Encoding.UTF8.GetBytes("""{"orderId":99}""");
        await store.EnqueueAsync(new RpcPendingRequest(
            pendingId,
            ProfileName,
            requestPayload,
            "application/json",
            DateTimeOffset.UtcNow));

        var configuration = CreateAsyncOutboxConfiguration();
        var serverTask = Task.Run(() => ServeOneAsyncOutboxRequest());
        var responseTask = Task.Run(() => CompleteOneResponseFromQueue(store));

        var relay = new RpcPendingRelayService(
            store,
            NullIntegrationLogger.Instance,
            new RpcPendingRelayOptions(),
            _ => configuration);
        await relay.RelayBatchAsync(batchSize: 10);

        var completed = await store.WaitForCompletionAsync(pendingId, TimeSpan.FromSeconds(15));

        await serverTask;
        await responseTask;

        Assert.NotNull(completed);
        Assert.Equal(RpcPendingStatus.Completed, completed!.Status);
        Assert.Equal("""{"status":"ok","orderId":99}""", Encoding.UTF8.GetString(completed.ResponsePayload!));
    }

    private void DeclareQueues()
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(RequestQueueName, durable: false, exclusive: false, autoDelete: true, arguments: null);
        channel.QueueDeclare(ResponseQueueName, durable: false, exclusive: false, autoDelete: true, arguments: null);
    }

    private RabbitMqRequestReplyConfiguration CreateAsyncOutboxConfiguration()
    {
        var factory = rabbitMq.CreateConnectionFactory();
        return new RabbitMqRequestReplyConfiguration
        {
            Name = ProfileName,
            HostName = factory.HostName,
            Port = factory.Port,
            UserName = factory.UserName,
            Password = factory.Password,
            VirtualHost = factory.VirtualHost,
            RequestTarget = RabbitMqRequestReplyTarget.Queue,
            QueueName = RequestQueueName,
            RequestMode = RabbitMqRequestReplyRequestMode.AsyncOutbox,
            ResponseQueueName = ResponseQueueName,
            ReplyMode = RabbitMqReplyMode.DirectReplyTo,
            ResponseTimeoutSeconds = 10,
            ValidateTopology = false,
            ContentType = "application/json",
            ClientProvidedName = "IntegrationFlow.RpcPendingE2E"
        };
    }

    private void ServeOneAsyncOutboxRequest()
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();

        BasicGetResult? delivery = null;
        for (var attempt = 0; attempt < 200 && delivery == null; attempt++)
        {
            delivery = channel.BasicGet(RequestQueueName, autoAck: false);
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

        Assert.Equal(ResponseQueueName, request.ReplyTo);

        var replyPublisher = new RabbitMqReplyPublisher(CreateAsyncOutboxConfiguration());
        replyPublisher.PublishTextReply(request, """{"status":"ok","orderId":99}""");

        channel.BasicAck(delivery.DeliveryTag, multiple: false);
    }

    private void CompleteOneResponseFromQueue(InMemoryRpcPendingStore store)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();

        BasicGetResult? delivery = null;
        for (var attempt = 0; attempt < 200 && delivery == null; attempt++)
        {
            delivery = channel.BasicGet(ResponseQueueName, autoAck: false);
            if (delivery == null)
            {
                Thread.Sleep(50);
            }
        }

        Assert.NotNull(delivery);
        Assert.True(Guid.TryParse(delivery!.BasicProperties?.CorrelationId, out var pendingId));

        store.CompleteAsync(pendingId, delivery.Body.ToArray()).GetAwaiter().GetResult();
        channel.BasicAck(delivery.DeliveryTag, multiple: false);
    }
}
