using IntegrationFlow.IntegrationTests.Infrastructure;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RabbitMqConnectivityTests : IAsyncLifetime
{
    private RabbitMqContainer? container;
    private bool dockerAvailable;

    public async Task InitializeAsync()
    {
        dockerAvailable = await DockerAvailability.IsAvailableAsync();
        if (!dockerAvailable)
        {
            return;
        }

        container = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management")
            .Build();

        await container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (container != null)
        {
            await container.DisposeAsync();
        }
    }

    [Fact]
    public async Task CanConnectAndDeclareQueue()
    {
        if (!dockerAvailable || container == null)
        {
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = container.Hostname,
            Port = container.GetMappedPublicPort(5672),
            UserName = "guest",
            Password = "guest"
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare("integration.test", durable: true, exclusive: false, autoDelete: true);
    }
}
