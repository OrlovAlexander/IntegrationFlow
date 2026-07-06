using IntegrationFlow.Testing;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace IntegrationFlow.IntegrationTests.Infrastructure;

internal sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    public RabbitMqContainer? Container { get; private set; }

    public bool DockerAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        DockerAvailable = await DockerAvailability.IsAvailableAsync();
        if (!DockerAvailable)
        {
            return;
        }

        Container = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management")
            .WithUsername(RabbitMqTestCredentials.Username)
            .WithPassword(RabbitMqTestCredentials.Password)
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (Container != null)
        {
            await Container.DisposeAsync();
        }
    }

    public ConnectionFactory CreateConnectionFactory()
    {
        if (Container == null)
        {
            throw new InvalidOperationException("RabbitMQ container is not started.");
        }

        return new ConnectionFactory
        {
            HostName = Container.Hostname,
            Port = Container.GetMappedPublicPort(5672),
            UserName = RabbitMqTestCredentials.Username,
            Password = RabbitMqTestCredentials.Password
        };
    }
}
