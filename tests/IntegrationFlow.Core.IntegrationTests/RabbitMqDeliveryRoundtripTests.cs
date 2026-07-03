using System.Text;
using IntegrationFlow.IntegrationTests.Infrastructure;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RabbitMqDeliveryRoundtripTests : IAsyncLifetime
{
    private const string QueueName = "integration.roundtrip";

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
    public async Task PublishAndConsumeMessage()
    {
        if (!dockerAvailable || container == null)
        {
            return;
        }

        var factory = CreateConnectionFactory();
        var body = Encoding.UTF8.GetBytes("roundtrip-payload");
        var messageId = Guid.NewGuid().ToString("N");

        using (var publishConnection = factory.CreateConnection())
        using (var publishChannel = publishConnection.CreateModel())
        {
            publishChannel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: true);
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
        if (!dockerAvailable || container == null)
        {
            return;
        }

        var factory = CreateConnectionFactory();
        var body = Encoding.UTF8.GetBytes("requeue-payload");

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: true);

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
        if (!dockerAvailable || container == null)
        {
            return;
        }

        var factory = CreateConnectionFactory();
        var messageId = "duplicate-msg-id";

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: true);

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

    private ConnectionFactory CreateConnectionFactory()
        => new()
        {
            HostName = container!.Hostname,
            Port = container.GetMappedPublicPort(5672),
            UserName = "guest",
            Password = "guest"
        };
}
