using System.Diagnostics;
using System.Text;
using System.Text.Json;
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

/// <summary>
/// T-4: load / concurrency coverage for sync RPC with MaxConcurrentRequests &gt; 1.
/// </summary>
[Trait("Category", "Integration")]
[Collection(RabbitMqIntegrationCollection.Name)]
public sealed class RabbitMqRequestReplyLoadTests : IAsyncLifetime
{
    private const string QueueName = "integration.rpc.load";
    private const int LoadMaxConcurrentRequests = 8;
    private const int LoadTotalRequests = 32;
    private const int ServerDelayMilliseconds = 50;

    private readonly RabbitMqContainerFixture rabbitMq = new();

    public Task InitializeAsync() => rabbitMq.InitializeAsync();

    public Task DisposeAsync() => rabbitMq.DisposeAsync();

    [Fact]
    public async Task TransmitAsync_UnderLoad_ReturnsUniqueResponsesForAllParallelRequests()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        DeclareRequestQueue();

        var configuration = CreateRuntimeConfiguration(
            responseTimeoutSeconds: 30,
            maxConcurrentRequests: LoadMaxConcurrentRequests,
            reuseConnection: true);

        using var connection = new RabbitMqRequestReplyConnection(configuration);
        var transmitter = new RabbitMqRequestReplyTransmitter(configuration, connection);

        using var serverCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var serverStats = new ConcurrentRpcServerStats();
        var serverTask = ServeConcurrentRequestsAsync(
            configuration,
            LoadTotalRequests,
            TimeSpan.FromMilliseconds(ServerDelayMilliseconds),
            serverWorkerCount: LoadMaxConcurrentRequests,
            serverStats,
            serverCts.Token);

        var stopwatch = Stopwatch.StartNew();
        var tasks = Enumerable.Range(1, LoadTotalRequests)
            .Select(orderId => transmitter.TransmitAsync(
                new TransmitData(CreateRequestPayload(orderId)),
                CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        await serverTask;

        Assert.All(Enumerable.Range(1, LoadTotalRequests), orderId =>
        {
            var result = results[orderId - 1];
            Assert.False(result.IsFailed);
            Assert.Equal(orderId, ExtractOrderId(result.Data));
        });

        Assert.True(
            serverStats.MaxConcurrentHandlers >= 2,
            $"Expected overlapping server handlers, observed max={serverStats.MaxConcurrentHandlers}.");
        Assert.True(
            serverStats.MaxConcurrentHandlers <= LoadMaxConcurrentRequests,
            $"Server concurrency exceeded client gate: max={serverStats.MaxConcurrentHandlers}, limit={LoadMaxConcurrentRequests}.");

        var serialBaseline = TimeSpan.FromMilliseconds(LoadTotalRequests * ServerDelayMilliseconds);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromTicks(serialBaseline.Ticks * 6 / 10),
            $"Expected parallel RPC faster than serial baseline. Elapsed={stopwatch.Elapsed.TotalMilliseconds:F0}ms, serial={serialBaseline.TotalMilliseconds:F0}ms.");
    }

    [Fact]
    public async Task TransmitAsync_DefaultMaxConcurrentRequests_SerializesClientRequests()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        const int requestCount = 6;
        const int delayMilliseconds = 80;

        DeclareRequestQueue();

        var configuration = CreateRuntimeConfiguration(
            responseTimeoutSeconds: 30,
            maxConcurrentRequests: 1,
            reuseConnection: true);

        using var connection = new RabbitMqRequestReplyConnection(configuration);
        var transmitter = new RabbitMqRequestReplyTransmitter(configuration, connection);

        using var serverCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var serverStats = new ConcurrentRpcServerStats();
        var serverTask = ServeConcurrentRequestsAsync(
            configuration,
            requestCount,
            TimeSpan.FromMilliseconds(delayMilliseconds),
            serverWorkerCount: 1,
            serverStats,
            serverCts.Token);

        var stopwatch = Stopwatch.StartNew();
        var tasks = Enumerable.Range(1, requestCount)
            .Select(orderId => transmitter.TransmitAsync(
                new TransmitData(CreateRequestPayload(orderId)),
                CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        await serverTask;

        var minimumSerialDuration = TimeSpan.FromMilliseconds(requestCount * delayMilliseconds * 0.85);
        Assert.True(
            stopwatch.Elapsed >= minimumSerialDuration,
            $"Expected serialized RPC under MaxConcurrentRequests=1. Elapsed={stopwatch.Elapsed.TotalMilliseconds:F0}ms, minimum={minimumSerialDuration.TotalMilliseconds:F0}ms.");
    }

    private async Task ServeConcurrentRequestsAsync(
        RabbitMqRequestReplyConfiguration configuration,
        int count,
        TimeSpan delayPerRequest,
        int serverWorkerCount,
        ConcurrentRpcServerStats stats,
        CancellationToken cancellationToken)
    {
        var served = 0;
        var workers = Enumerable.Range(0, serverWorkerCount)
            .Select(_ => Task.Run(
                () => ServeWorkerLoop(
                    configuration,
                    count,
                    delayPerRequest,
                    stats,
                    ref served,
                    cancellationToken),
                cancellationToken))
            .ToArray();

        await Task.WhenAll(workers).ConfigureAwait(false);

        if (Volatile.Read(ref served) != count)
        {
            throw new InvalidOperationException(
                $"Server processed {Volatile.Read(ref served)} of {count} RPC requests.");
        }
    }

    private void ServeWorkerLoop(
        RabbitMqRequestReplyConfiguration configuration,
        int totalCount,
        TimeSpan delayPerRequest,
        ConcurrentRpcServerStats stats,
        ref int servedCount,
        CancellationToken cancellationToken)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();

        while (!cancellationToken.IsCancellationRequested)
        {
            if (Volatile.Read(ref servedCount) >= totalCount)
            {
                break;
            }

            BasicGetResult? delivery = channel.BasicGet(QueueName, autoAck: false);
            if (delivery == null)
            {
                Thread.Sleep(25);
                continue;
            }

            stats.Enter();
            try
            {
                var body = delivery.Body.ToArray();
                var orderId = ExtractOrderId(body);
                var request = new RabbitMqReceivedMessage(
                    body,
                    delivery.DeliveryTag,
                    delivery.RoutingKey,
                    delivery.BasicProperties?.MessageId,
                    delivery.BasicProperties?.CorrelationId,
                    delivery.BasicProperties?.ReplyTo);

                if (delayPerRequest > TimeSpan.Zero)
                {
                    Thread.Sleep(delayPerRequest);
                }

                var replyPublisher = new RabbitMqReplyPublisher(configuration);
                replyPublisher.PublishTextReply(request, CreateResponsePayload(orderId));
                channel.BasicAck(delivery.DeliveryTag, multiple: false);
                Interlocked.Increment(ref servedCount);
            }
            finally
            {
                stats.Exit();
            }
        }
    }

    private RabbitMqRequestReplyConfiguration CreateRuntimeConfiguration(
        int responseTimeoutSeconds,
        int maxConcurrentRequests,
        bool reuseConnection)
    {
        var factory = rabbitMq.CreateConnectionFactory();
        return new RabbitMqRequestReplyConfiguration
        {
            Name = "OrdersRpcLoad",
            HostName = factory.HostName,
            Port = factory.Port,
            UserName = factory.UserName,
            Password = factory.Password,
            VirtualHost = factory.VirtualHost,
            RequestTarget = RabbitMqRequestReplyTarget.Queue,
            QueueName = QueueName,
            ReplyMode = RabbitMqReplyMode.DirectReplyTo,
            ResponseTimeoutSeconds = responseTimeoutSeconds,
            MaxConcurrentRequests = maxConcurrentRequests,
            ReuseConnection = reuseConnection,
            ValidateTopology = true,
            ContentType = "application/json",
            ClientProvidedName = "IntegrationFlow.RequestReplyLoad"
        };
    }

    private void DeclareRequestQueue()
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(QueueName, durable: false, exclusive: false, autoDelete: true, arguments: null);
    }

    private static string CreateRequestPayload(int orderId)
        => JsonSerializer.Serialize(new { orderId });

    private static string CreateResponsePayload(int orderId)
        => JsonSerializer.Serialize(new { status = "ok", orderId });

    private static int ExtractOrderId(object? data)
    {
        var json = data switch
        {
            null => throw new InvalidOperationException("Response payload is null."),
            string text => text,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => data.ToString() ?? throw new InvalidOperationException("Unsupported response payload.")
        };

        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("orderId").GetInt32();
    }

    private sealed class ConcurrentRpcServerStats
    {
        private int current;
        private int max;

        public int MaxConcurrentHandlers => Volatile.Read(ref max);

        public void Enter()
        {
            var observed = Interlocked.Increment(ref current);
            UpdateMax(observed);
        }

        public void Exit()
            => Interlocked.Decrement(ref current);

        private void UpdateMax(int observed)
        {
            while (true)
            {
                var snapshot = Volatile.Read(ref max);
                if (observed <= snapshot)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref max, observed, snapshot) == snapshot)
                {
                    return;
                }
            }
        }
    }
}
