using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.DependencyInjection;
using IntegrationFlow.IntegrationTests.Infrastructure;
using IntegrationFlow.Testing;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RabbitMqListenerLegacyEndToEndTests : IAsyncLifetime
{
    private const string ProfileName = "LegacyE2E";
    private const string QueueName = "integration.legacy.listener.e2e";

    private readonly RabbitMqContainerFixture rabbitMq = new();

    public Task InitializeAsync() => rabbitMq.InitializeAsync();

    public Task DisposeAsync() => rabbitMq.DisposeAsync();

    [Fact]
    public async Task BeginReceiving_ProcessesMessageViaLegacyPublisherPath()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        ResetProcessorState();
        DeclareQueue();
        WriteConsumeProfile();

        var publisher = CreatePublisher();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        publisher.BeginReceiving(() => started.TrySetResult());

        await started.Task.WaitAsync(TimeSpan.FromSeconds(15));

        try
        {
            Publish("payload-legacy", "msg-legacy-1");
            await WaitForProcessCountAsync(1, TimeSpan.FromSeconds(15));

            Assert.Equal(1, EndToEndProcessorSide.ProcessCallCount);
        }
        finally
        {
            publisher.StopReceiving();
        }
    }

    [Fact]
    public async Task BeginReceiving_StopReceiving_CompletesWithoutHang()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        ResetProcessorState();
        DeclareQueue();
        WriteConsumeProfile();

        var publisher = CreatePublisher();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        publisher.BeginReceiving(() => started.TrySetResult());

        await started.Task.WaitAsync(TimeSpan.FromSeconds(15));

        publisher.StopReceiving();
        await Task.Delay(100);
    }

    private static PublisherBase CreatePublisher()
    {
        var logger = NullIntegrationLogger.Instance;
        var publisher = PublisherBase.Create<RabbitMqPublisher>(
            logger,
            new EndToEndRabbitMqPublisherSide(ProfileName));
        publisher.Metrics = NullIntegrationFlowMetrics.Instance;
        return publisher;
    }

    private static void ResetProcessorState()
    {
        EndToEndProcessorSide.ProcessCallCount = 0;
        EndToEndProcessorSide.ShouldThrow = false;
        EndToEndProcessorSide.CurrentStore = null;
    }

    private void DeclareQueue()
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(QueueName, durable: false, exclusive: false, autoDelete: false);
    }

    private void WriteConsumeProfile()
    {
        TempRabbitMqConfigWriter.WriteConsumeProfile(
            ProfileName,
            QueueName,
            rabbitMq.Container!.Hostname,
            rabbitMq.Container.GetMappedPublicPort(5672));
    }

    private void Publish(string payload, string messageId)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        var properties = channel.CreateBasicProperties();
        properties.MessageId = messageId;
        channel.BasicPublish(string.Empty, QueueName, properties, System.Text.Encoding.UTF8.GetBytes(payload));
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
}
