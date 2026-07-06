using IntegrationFlow.IntegrationTests.Infrastructure;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(RabbitMqIntegrationCollection.Name)]
public sealed class RabbitMqConnectivityTests : IAsyncLifetime
{
    private readonly RabbitMqContainerFixture rabbitMq = new();

    public Task InitializeAsync() => rabbitMq.InitializeAsync();

    public Task DisposeAsync() => rabbitMq.DisposeAsync();

    [Fact]
    public async Task CanConnectAndDeclareQueue()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare("integration.test", durable: true, exclusive: false, autoDelete: true, arguments: null);
    }
}
