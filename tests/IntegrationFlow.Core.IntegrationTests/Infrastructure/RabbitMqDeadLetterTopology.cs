using RabbitMQ.Client;

namespace IntegrationFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Retry + DLQ topology for E2E tests: work queue dead-letters to retry (TTL) and back until final DLQ.
/// </summary>
internal sealed class RabbitMqDeadLetterTopology
{
    public const string ExchangeName = "integration.dlx.e2e";
    public const string WorkQueueName = "integration.work.e2e";
    public const string RetryQueueName = "integration.work.retry.e2e";
    public const string DeadLetterQueueName = "integration.work.dlq.e2e";
    public const string WorkRoutingKey = "work";
    public const string RetryRoutingKey = "retry";
    public const string DeadLetterRoutingKey = "dlq";

    public static void Declare(IConnection connection, int retryTtlMilliseconds = 50, bool retryRoutesToDeadLetter = false)
    {
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: false, autoDelete: false);

        channel.QueueDeclare(
            DeadLetterQueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(DeadLetterQueueName, ExchangeName, DeadLetterRoutingKey);

        var retryDeadLetterRoutingKey = retryRoutesToDeadLetter ? DeadLetterRoutingKey : WorkRoutingKey;
        channel.QueueDeclare(
            RetryQueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = ExchangeName,
                ["x-dead-letter-routing-key"] = retryDeadLetterRoutingKey,
                ["x-message-ttl"] = retryTtlMilliseconds,
            });
        channel.QueueBind(RetryQueueName, ExchangeName, RetryRoutingKey);

        channel.QueueDeclare(
            WorkQueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = ExchangeName,
                ["x-dead-letter-routing-key"] = RetryRoutingKey,
            });
        channel.QueueBind(WorkQueueName, ExchangeName, WorkRoutingKey);
    }

    public static void PublishToWorkQueue(IConnection connection, byte[] body, string messageId)
    {
        PublishToWorkQueue(connection, body, messageId, headers: null);
    }

    public static bool TryGetFromQueue(IConnection connection, string queueName, out byte[]? body, out string? messageId)
    {
        using var channel = connection.CreateModel();
        var delivery = channel.BasicGet(queueName, autoAck: true);
        if (delivery == null)
        {
            body = null;
            messageId = null;
            return false;
        }

        body = delivery.Body.ToArray();
        messageId = delivery.BasicProperties?.MessageId;
        return true;
    }

    public static int GetQueueMessageCount(IConnection connection, string queueName)
    {
        using var channel = connection.CreateModel();
        var declare = channel.QueueDeclarePassive(queueName);
        return (int)declare.MessageCount;
    }

    public static void Delete(IConnection connection)
    {
        using var channel = connection.CreateModel();
        channel.QueueDelete(WorkQueueName, ifUnused: false, ifEmpty: false);
        channel.QueueDelete(RetryQueueName, ifUnused: false, ifEmpty: false);
        channel.QueueDelete(DeadLetterQueueName, ifUnused: false, ifEmpty: false);
        channel.ExchangeDelete(ExchangeName, ifUnused: false);
    }

    public static void PublishToWorkQueue(
        IConnection connection,
        byte[] body,
        string messageId,
        IDictionary<string, object>? headers = null)
    {
        using var channel = connection.CreateModel();
        var properties = channel.CreateBasicProperties();
        properties.MessageId = messageId;
        if (headers is { Count: > 0 })
        {
            properties.Headers = new Dictionary<string, object>(headers);
        }

        channel.BasicPublish(ExchangeName, WorkRoutingKey, properties, body);
    }
}
