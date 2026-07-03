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
public sealed class RabbitMqListenerHostedEndToEndTests : IAsyncLifetime
{
    private const string ProfileName = "HostedE2E";
    private const string QueueName = "integration.hosted.listener.e2e";

    private readonly RabbitMqContainerFixture rabbitMq = new();

    public Task InitializeAsync() => rabbitMq.InitializeAsync();

    public Task DisposeAsync() => rabbitMq.DisposeAsync();

    [Fact]
    public async Task HostedService_StartsConsumerAndProcessesMessage()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        ResetProcessorState();
        DeclareQueue();
        WriteConsumeProfile();

        var host = BuildHost(ProfileName);
        await host.StartAsync();
        try
        {
            Publish("payload-hosted", "msg-hosted-1");
            await WaitForProcessCountAsync(1, TimeSpan.FromSeconds(15));

            Assert.Equal(1, EndToEndProcessorSide.ProcessCallCount);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Fact]
    public async Task HostedService_StopAsync_CompletesWithoutHang()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        ResetProcessorState();
        DeclareQueue();
        WriteConsumeProfile();

        var host = BuildHost(ProfileName);
        await host.StartAsync();
        try
        {
            var stopTask = host.StopAsync();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(stopTask.IsCompletedSuccessfully);
        }
        finally
        {
            host.Dispose();
        }
    }

    [Fact]
    public async Task HostedService_MultiProfile_TwoQueuesIndependent()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        const string profileA = "HostedE2E_A";
        const string profileB = "HostedE2E_B";
        const string queueA = "integration.hosted.a";
        const string queueB = "integration.hosted.b";

        ResetProcessorState();
        DeclareQueue(queueA);
        DeclareQueue(queueB);
        TempRabbitMqConfigWriter.WriteConsumeProfiles(
            new[] { (profileA, queueA), (profileB, queueB) },
            rabbitMq.Container.Hostname,
            rabbitMq.Container.GetMappedPublicPort(5672));

        var host = BuildHost(profileA, profileB);
        await host.StartAsync();
        try
        {
            Publish(queueA, "payload-a", "msg-a");
            Publish(queueB, "payload-b", "msg-b");
            await WaitForProcessCountAsync(2, TimeSpan.FromSeconds(15));

            Assert.Equal(2, EndToEndProcessorSide.ProcessCallCount);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    private static IHost BuildHost(params string[] profileNames)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddIntegrationFlow();

                foreach (var profileName in profileNames)
                {
                    services.AddIntegrationFlowRabbitMqListener(
                        profileName,
                        _ => new DelegateInboxMessageProcessing(_ =>
                        {
                            EndToEndProcessorSide.ProcessCallCount++;
                            if (EndToEndProcessorSide.ShouldThrow)
                            {
                                throw new InvalidOperationException("processing failed");
                            }
                        }),
                        sp => EndToEndProcessorSide.CurrentStore);
                }
            })
            .Build();
    }

    private static void ResetProcessorState()
    {
        EndToEndProcessorSide.ProcessCallCount = 0;
        EndToEndProcessorSide.ShouldThrow = false;
        EndToEndProcessorSide.CurrentStore = null;
    }

    private void DeclareQueue(string queueName = QueueName)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(queueName, durable: false, exclusive: false, autoDelete: false);
    }

    private void WriteConsumeProfile(string profileName = ProfileName, string queueName = QueueName)
    {
        TempRabbitMqConfigWriter.WriteConsumeProfile(
            profileName,
            queueName,
            rabbitMq.Container!.Hostname,
            rabbitMq.Container.GetMappedPublicPort(5672));
    }

    private void Publish(string payload, string messageId, string queueName = QueueName)
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        var properties = channel.CreateBasicProperties();
        properties.MessageId = messageId;
        channel.BasicPublish(string.Empty, queueName, properties, System.Text.Encoding.UTF8.GetBytes(payload));
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
