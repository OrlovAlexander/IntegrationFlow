using System.Text;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.DependencyInjection;
using IntegrationFlow.IntegrationTests.Infrastructure;
using IntegrationFlow.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(RabbitMqIntegrationCollection.Name)]
public sealed class RabbitMqDeadLetterEndToEndTests : IAsyncLifetime
{
    private const string ProfileName = "DlqE2E";

    private readonly RabbitMqContainerFixture rabbitMq = new();

    public Task InitializeAsync() => rabbitMq.InitializeAsync();

    public Task DisposeAsync() => rabbitMq.DisposeAsync();

    [Fact]
    public async Task HostedService_PoisonMessage_RoutesToDeadLetterQueue()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        ResetProcessorState(shouldThrow: true);
        DeclareDeadLetterTopology();
        WriteConsumeProfile(requeueOnFailure: false);

        var host = BuildHost();
        await host.StartAsync();
        try
        {
            PublishToWorkQueue("poison-payload", "msg-poison-1");
            await WaitForProcessCountAsync(1, TimeSpan.FromSeconds(15));

            using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
            Assert.Equal(0, RabbitMqDeadLetterTopology.GetQueueMessageCount(connection, RabbitMqDeadLetterTopology.WorkQueueName));
            Assert.True(
                RabbitMqDeadLetterTopology.TryGetFromQueue(
                    connection,
                    RabbitMqDeadLetterTopology.DeadLetterQueueName,
                    out var body,
                    out var messageId));
            Assert.Equal("msg-poison-1", messageId);
            Assert.Equal("poison-payload", Encoding.UTF8.GetString(body!));
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Fact]
    public async Task HostedService_MaxRetryCountWithDeathHeader_RoutesToDeadLetterQueue()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        DeclareRetryTopology(retryRoutesToDeadLetter: false);
        WriteConsumeProfile(requeueOnFailure: false, maxRetryCount: 999);
        PublishToWorkQueue("retry-exhausted", "msg-retry-exhausted");

        var host = BuildHost();
        ResetProcessorState(shouldThrow: true);
        await host.StartAsync();
        try
        {
            await WaitForProcessCountAsync(2, TimeSpan.FromSeconds(60));
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }

        byte[] body;
        string messageId;
        IDictionary<string, object>? headers;
        using (var connection = rabbitMq.CreateConnectionFactory().CreateConnection())
        using (var channel = connection.CreateModel())
        {
            var delivery = await WaitForBasicGetAsync(channel, RabbitMqDeadLetterTopology.WorkQueueName, TimeSpan.FromSeconds(15));
            body = delivery.Body.ToArray();
            messageId = delivery.BasicProperties.MessageId ?? "msg-retry-exhausted";
            headers = delivery.BasicProperties.Headers;
            channel.BasicAck(delivery.DeliveryTag, false);

            RabbitMqDeadLetterTopology.Delete(connection);
        }

        DeclareDeadLetterTopology();
        WriteConsumeProfile(requeueOnFailure: true, maxRetryCount: 2);
        using (var connection = rabbitMq.CreateConnectionFactory().CreateConnection())
        {
            RabbitMqDeadLetterTopology.PublishToWorkQueue(connection, body, messageId, headers);
        }

        ResetProcessorState(shouldThrow: true);
        host = BuildHost();
        await host.StartAsync();
        try
        {
            await WaitForProcessCountAsync(1, TimeSpan.FromSeconds(15));

            using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
            Assert.Equal(0, RabbitMqDeadLetterTopology.GetQueueMessageCount(connection, RabbitMqDeadLetterTopology.WorkQueueName));
            Assert.True(
                RabbitMqDeadLetterTopology.TryGetFromQueue(
                    connection,
                    RabbitMqDeadLetterTopology.DeadLetterQueueName,
                    out _,
                    out var dlqMessageId));
            Assert.Equal(messageId, dlqMessageId);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Fact]
    public async Task RetryTopology_FailedMessage_AccumulatesDeathCountAndReachesDlq()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        ResetProcessorState(shouldThrow: true);
        DeclareRetryTopology(retryRoutesToDeadLetter: true);
        WriteConsumeProfile(requeueOnFailure: false);

        var host = BuildHost();
        await host.StartAsync();
        try
        {
            PublishToWorkQueue("retry-cycle", "msg-retry-cycle");
            await WaitForProcessCountAsync(1, TimeSpan.FromSeconds(15));
            await WaitForDlqMessageAsync("msg-retry-cycle", TimeSpan.FromSeconds(30));

            using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
            Assert.Equal(0, RabbitMqDeadLetterTopology.GetQueueMessageCount(connection, RabbitMqDeadLetterTopology.WorkQueueName));
            Assert.Equal(0, RabbitMqDeadLetterTopology.GetQueueMessageCount(connection, RabbitMqDeadLetterTopology.RetryQueueName));
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    private static IHost BuildHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddIntegrationFlow();
                services.AddIntegrationFlowRabbitMqListener(
                    ProfileName,
                    _ => new DelegateInboxMessageProcessing(_ =>
                    {
                        EndToEndProcessorSide.ProcessCallCount++;
                        if (EndToEndProcessorSide.ShouldThrow)
                        {
                            throw new InvalidOperationException("processing failed");
                        }
                    }));
            })
            .Build();
    }

    private static void ResetProcessorState(bool shouldThrow)
    {
        EndToEndProcessorSide.ProcessCallCount = 0;
        EndToEndProcessorSide.ShouldThrow = shouldThrow;
        EndToEndProcessorSide.CurrentStore = null;
    }

    private void DeclareDeadLetterTopology()
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(RabbitMqDeadLetterTopology.ExchangeName, ExchangeType.Direct, durable: false, autoDelete: false);

        channel.QueueDeclare(
            RabbitMqDeadLetterTopology.DeadLetterQueueName,
            durable: false,
            exclusive: false,
            autoDelete: false);
        channel.QueueBind(
            RabbitMqDeadLetterTopology.DeadLetterQueueName,
            RabbitMqDeadLetterTopology.ExchangeName,
            RabbitMqDeadLetterTopology.DeadLetterRoutingKey);

        channel.QueueDeclare(
            RabbitMqDeadLetterTopology.WorkQueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = RabbitMqDeadLetterTopology.ExchangeName,
                ["x-dead-letter-routing-key"] = RabbitMqDeadLetterTopology.DeadLetterRoutingKey,
            });
        channel.QueueBind(
            RabbitMqDeadLetterTopology.WorkQueueName,
            RabbitMqDeadLetterTopology.ExchangeName,
            RabbitMqDeadLetterTopology.WorkRoutingKey);
    }

    private void DeclareRetryTopology(bool retryRoutesToDeadLetter = false)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        RabbitMqDeadLetterTopology.Declare(connection, retryTtlMilliseconds: 100, retryRoutesToDeadLetter: retryRoutesToDeadLetter);
    }

    private void WriteConsumeProfile(bool requeueOnFailure, int maxRetryCount = 0)
    {
        TempRabbitMqConfigWriter.WriteConsumeProfile(
            ProfileName,
            RabbitMqDeadLetterTopology.WorkQueueName,
            rabbitMq.Container!.Hostname,
            rabbitMq.Container.GetMappedPublicPort(5672),
            requeueOnFailure: requeueOnFailure,
            maxRetryCount: maxRetryCount);
    }

    private void PublishToWorkQueue(string payload, string messageId)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        RabbitMqDeadLetterTopology.PublishToWorkQueue(
            connection,
            Encoding.UTF8.GetBytes(payload),
            messageId);
    }

    private static async Task<BasicGetResult> WaitForBasicGetAsync(IModel channel, string queueName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var delivery = channel.BasicGet(queueName, autoAck: false);
            if (delivery != null)
            {
                return delivery;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"No message in queue '{queueName}' within {timeout}.");
    }

    private static async Task WaitForProcessCountAsync(int expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (EndToEndProcessorSide.ProcessCallCount >= expected)
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Expected ProcessCallCount >= {expected}, actual {EndToEndProcessorSide.ProcessCallCount}.");
    }

    private async Task WaitForDlqMessageAsync(string messageId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
            using var channel = connection.CreateModel();
            var delivery = channel.BasicGet(RabbitMqDeadLetterTopology.DeadLetterQueueName, autoAck: false);
            if (delivery != null)
            {
                var foundId = delivery.BasicProperties?.MessageId;
                if (foundId == messageId)
                {
                    channel.BasicAck(delivery.DeliveryTag, false);
                    return;
                }

                channel.BasicNack(delivery.DeliveryTag, false, true);
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Message '{messageId}' did not reach DLQ within {timeout}.");
    }
}
