using System.Collections;
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

        ResetProcessorState(shouldThrow: true);
        DeclareDeadLetterTopology();
        WriteConsumeProfile(requeueOnFailure: true, maxRetryCount: 2);

        var host = BuildHost();
        await host.StartAsync();
        try
        {
            PublishToWorkQueueWithDeathCount("retry-exhausted", "msg-retry-exhausted", deathCount: 2);
            await WaitForProcessCountAsync(1, TimeSpan.FromSeconds(15));

            using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
            Assert.Equal(0, RabbitMqDeadLetterTopology.GetQueueMessageCount(connection, RabbitMqDeadLetterTopology.WorkQueueName));
            Assert.True(
                RabbitMqDeadLetterTopology.TryGetFromQueue(
                    connection,
                    RabbitMqDeadLetterTopology.DeadLetterQueueName,
                    out _,
                    out var messageId));
            Assert.Equal("msg-retry-exhausted", messageId);
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
        DeclareRetryTopology();
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

        channel.ExchangeDeclare(RabbitMqDeadLetterTopology.ExchangeName, ExchangeType.Direct, durable: false, autoDelete: true);

        channel.QueueDeclare(
            RabbitMqDeadLetterTopology.DeadLetterQueueName,
            durable: false,
            exclusive: false,
            autoDelete: true);
        channel.QueueBind(
            RabbitMqDeadLetterTopology.DeadLetterQueueName,
            RabbitMqDeadLetterTopology.ExchangeName,
            RabbitMqDeadLetterTopology.DeadLetterRoutingKey);

        channel.QueueDeclare(
            RabbitMqDeadLetterTopology.WorkQueueName,
            durable: false,
            exclusive: false,
            autoDelete: true,
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

    private void DeclareRetryTopology()
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        RabbitMqDeadLetterTopology.Declare(connection, retryTtlMilliseconds: 100, retryRoutesToDeadLetter: true);
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

    private void PublishToWorkQueueWithDeathCount(string payload, string messageId, int deathCount)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        var properties = channel.CreateBasicProperties();
        properties.MessageId = messageId;
        properties.Headers = new Dictionary<string, object>
        {
            ["x-death"] = new ArrayList
            {
                new Dictionary<string, object>
                {
                    ["count"] = (long)deathCount,
                    ["reason"] = "rejected",
                    ["queue"] = RabbitMqDeadLetterTopology.WorkQueueName,
                    ["exchange"] = RabbitMqDeadLetterTopology.ExchangeName,
                }
            }
        };

        channel.BasicPublish(
            RabbitMqDeadLetterTopology.ExchangeName,
            RabbitMqDeadLetterTopology.WorkRoutingKey,
            properties,
            Encoding.UTF8.GetBytes(payload));
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
