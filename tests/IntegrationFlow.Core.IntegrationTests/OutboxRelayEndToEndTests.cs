using System.Text;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using IntegrationFlow.IntegrationTests.Infrastructure;
using IntegrationFlow.Testing;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(RabbitMqIntegrationCollection.Name)]
public sealed class OutboxRelayEndToEndTests : IAsyncLifetime
{
    private const string ProfileName = "OrdersOut";
    private const string QueueName = "integration.outbox.relay";

    private readonly RabbitMqContainerFixture rabbitMq = new();

    public Task InitializeAsync() => rabbitMq.InitializeAsync();

    public Task DisposeAsync() => rabbitMq.DisposeAsync();

    [Fact]
    public async Task RelayBatchAsync_PublishesClaimedOutboxMessageToRabbitMq()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        await using var dbFactory = TestDbContextFactoryFactory.Create($"outbox-relay-{Guid.NewGuid():N}");
        var store = new EfOutboxStore<TestIntegrationDbContext>(dbFactory);
        var outboxId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("outbox-relay-payload");

        WritePublishConfig();
        DeclareQueue();

        await store.EnqueueAsync(new OutboxMessage(
            outboxId,
            ProfileName,
            payload,
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount: 0));

        var relay = CreateRelayService(store);
        await relay.RelayBatchAsync(batchSize: 10);

        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        var result = channel.BasicGet(QueueName, autoAck: true);

        Assert.NotNull(result);
        Assert.Equal(outboxId.ToString("N"), result!.BasicProperties.MessageId);
        Assert.Equal(payload, result.Body.ToArray());
    }

    [Fact]
    public async Task RelayBatchAsync_MarkPublished_PreventsRedeliveryOnSecondRelay()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        await using var dbFactory = TestDbContextFactoryFactory.Create($"outbox-relay-{Guid.NewGuid():N}");
        var store = new EfOutboxStore<TestIntegrationDbContext>(dbFactory);
        var outboxId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("single-delivery-payload");

        WritePublishConfig();
        DeclareQueue();

        await store.EnqueueAsync(new OutboxMessage(
            outboxId,
            ProfileName,
            payload,
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount: 0));

        var relay = CreateRelayService(store);
        await relay.RelayBatchAsync(batchSize: 10);
        await relay.RelayBatchAsync(batchSize: 10);

        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();

        var firstDelivery = channel.BasicGet(QueueName, autoAck: true);
        var secondDelivery = channel.BasicGet(QueueName, autoAck: true);

        Assert.NotNull(firstDelivery);
        Assert.Equal(outboxId.ToString("N"), firstDelivery!.BasicProperties.MessageId);
        Assert.Null(secondDelivery);
    }

    private static OutboxRelayService CreateRelayService(EfOutboxStore<TestIntegrationDbContext> store)
        => new(store, NullIntegrationLogger.Instance, new OutboxRelayOptions());

    private void WritePublishConfig()
    {
        TempRabbitMqConfigWriter.WritePublishProfile(
            ProfileName,
            QueueName,
            rabbitMq.Container!.Hostname,
            rabbitMq.Container.GetMappedPublicPort(5672));
    }

    private void DeclareQueue()
    {
        using var connection = rabbitMq.CreateConnectionFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: true, arguments: null);
    }
}
