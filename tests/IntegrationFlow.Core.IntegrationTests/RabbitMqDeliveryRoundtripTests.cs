using System.Text;
using IntegrationFlow.IntegrationTests.Infrastructure;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(RabbitMqIntegrationCollection.Name)]
public sealed class RabbitMqDeliveryRoundtripTests : IAsyncLifetime
{
    private const string QueueName = "integration.roundtrip";

    private readonly RabbitMqContainerFixture rabbitMq = new();

    public Task InitializeAsync() => rabbitMq.InitializeAsync();

    public Task DisposeAsync() => rabbitMq.DisposeAsync();

    [Fact]
    public async Task PublishAndConsumeMessage()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        var factory = rabbitMq.CreateConnectionFactory();
        var body = Encoding.UTF8.GetBytes("roundtrip-payload");
        var messageId = Guid.NewGuid().ToString("N");

        using (var publishConnection = factory.CreateConnection())
        using (var publishChannel = publishConnection.CreateModel())
        {
            publishChannel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: true, arguments: null);
            var properties = publishChannel.CreateBasicProperties();
            properties.MessageId = messageId;
            properties.DeliveryMode = 2;
            publishChannel.BasicPublish(string.Empty, QueueName, properties, body);
        }

        using var consumeConnection = factory.CreateConnection();
        using var consumeChannel = consumeConnection.CreateModel();
        var result = consumeChannel.BasicGet(QueueName, autoAck: true);

        Assert.NotNull(result);
        Assert.Equal(messageId, result!.BasicProperties.MessageId);
        Assert.Equal(body, result.Body.ToArray());
    }

    [Fact]
    public async Task NackWithRequeue_RedeliversMessage()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        var factory = rabbitMq.CreateConnectionFactory();
        var body = Encoding.UTF8.GetBytes("requeue-payload");

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: true, arguments: null);

        channel.BasicPublish(string.Empty, QueueName, null, body);

        var firstDelivery = channel.BasicGet(QueueName, autoAck: false);
        Assert.NotNull(firstDelivery);
        channel.BasicNack(firstDelivery!.DeliveryTag, false, true);

        var secondDelivery = channel.BasicGet(QueueName, autoAck: true);
        Assert.NotNull(secondDelivery);
        Assert.Equal(body, secondDelivery!.Body.ToArray());
    }

    [Fact]
    public async Task DuplicateMessageId_CanBeDetectedByConsumer()
    {
        if (!rabbitMq.DockerAvailable || rabbitMq.Container == null)
        {
            return;
        }

        var factory = rabbitMq.CreateConnectionFactory();
        var messageId = "duplicate-msg-id";

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: true, arguments: null);

        for (var index = 0; index < 2; index++)
        {
            var properties = channel.CreateBasicProperties();
            properties.MessageId = messageId;
            channel.BasicPublish(string.Empty, QueueName, properties, Encoding.UTF8.GetBytes("dup"));
        }

        var firstDelivery = channel.BasicGet(QueueName, autoAck: true);
        var secondDelivery = channel.BasicGet(QueueName, autoAck: true);

        Assert.NotNull(firstDelivery);
        Assert.NotNull(secondDelivery);
        Assert.Equal(messageId, firstDelivery!.BasicProperties.MessageId);
        Assert.Equal(messageId, secondDelivery!.BasicProperties.MessageId);

        var processed = new HashSet<string>(StringComparer.Ordinal);
        Assert.True(processed.Add(firstDelivery.BasicProperties.MessageId!));
        Assert.False(processed.Add(secondDelivery.BasicProperties.MessageId!));
    }
}
