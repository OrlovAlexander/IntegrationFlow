using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RabbitMqConnectivityTests : IAsyncLifetime
{
    private RabbitMqContainer? container;

    public async Task InitializeAsync()
    {
        if (!IsDockerAvailable())
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
        if (container == null)
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

    private static bool IsDockerAvailable()
    {
        try
        {
            return Environment.GetEnvironmentVariable("DOCKER_HOST") != null ||
                   File.Exists("/var/run/docker.sock") ||
                   OperatingSystem.IsWindows();
        }
        catch
        {
            return false;
        }
    }
}
