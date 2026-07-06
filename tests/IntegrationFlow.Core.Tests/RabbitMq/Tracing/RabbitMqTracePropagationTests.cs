using System.Collections;
using System.Diagnostics;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.Tracing;

public sealed class RabbitMqTracePropagationTests
{
    [Fact]
    public void Inject_WritesTraceParentAndTraceStateHeaders()
    {
        using var listener = RegisterActivityListener();
        using var activity = CreateActivitySource().StartActivity("test.publish", ActivityKind.Producer);
        Assert.NotNull(activity);

        activity!.TraceStateString = "vendor=value";
        var properties = new TestBasicProperties();

        RabbitMqTracePropagation.Inject(properties);

        Assert.NotNull(properties.Headers);
        Assert.True(properties.Headers.ContainsKey(RabbitMqTraceHeaders.TraceParent));
        Assert.Equal(
            RabbitMqTracePropagation.BuildTraceParent(activity),
            properties.Headers[RabbitMqTraceHeaders.TraceParent]);
        Assert.Equal("vendor=value", properties.Headers[RabbitMqTraceHeaders.TraceState]);
    }

    [Fact]
    public void Inject_WhenNoCurrentActivity_DoesNotCreateHeaders()
    {
        var properties = new TestBasicProperties();

        RabbitMqTracePropagation.Inject(properties);

        Assert.Null(properties.Headers);
    }

    [Fact]
    public void TryExtractParentContext_RoundTripsInjectedHeader()
    {
        using var listener = RegisterActivityListener();
        using var producer = CreateActivitySource().StartActivity("test.publish", ActivityKind.Producer);
        Assert.NotNull(producer);

        var properties = new TestBasicProperties();
        RabbitMqTracePropagation.Inject(properties);

        var extracted = RabbitMqTracePropagation.TryExtractParentContext(properties.Headers, out var parentContext);

        Assert.True(extracted);
        Assert.Equal(producer!.TraceId, parentContext.TraceId);
        Assert.Equal(producer.SpanId, parentContext.SpanId);
    }

    [Fact]
    public void StartConsumerActivity_LinksToExtractedParentTrace()
    {
        using var listener = RegisterActivityListener();
        using var producer = CreateActivitySource().StartActivity("test.publish", ActivityKind.Producer);
        Assert.NotNull(producer);

        var properties = new TestBasicProperties();
        RabbitMqTracePropagation.Inject(properties);

        using var consumer = RabbitMqDistributedTracing.StartConsumerActivity(
            properties.Headers,
            "receive",
            "Inbox",
            "msg-1",
            "corr-1",
            42);

        Assert.NotNull(consumer);
        Assert.Equal(producer!.TraceId, consumer!.TraceId);
        Assert.NotEqual(producer.SpanId, consumer.SpanId);
        Assert.Equal("rabbitmq.receive", consumer.OperationName);
        Assert.Equal("Inbox", consumer.Tags.First(tag => tag.Key == "integrationflow.profile").Value);
        Assert.Equal("42", consumer.GetTagItem("messaging.rabbitmq.delivery_tag")?.ToString());
    }

    [Fact]
    public void TryGetStringHeader_ReadsByteArrayHeader()
    {
        var headers = new Dictionary<string, object>
        {
            [RabbitMqTraceHeaders.TraceParent] = System.Text.Encoding.UTF8.GetBytes("00-abc-def-01"),
        };

        var found = RabbitMqTracePropagation.TryGetStringHeader(
            headers,
            RabbitMqTraceHeaders.TraceParent,
            out var value);

        Assert.True(found);
        Assert.Equal("00-abc-def-01", value);
    }

    private static ActivityListener RegisterActivityListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == IntegrationFlowRabbitMqActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static ActivitySource CreateActivitySource()
        => new(IntegrationFlowRabbitMqActivitySource.Name, IntegrationFlowRabbitMqActivitySource.Version);

    private sealed class TestBasicProperties : IBasicProperties
    {
        public ushort ProtocolClassId => 60;

        public string ProtocolClassName => "amq.basic";

        public string? AppId { get; set; }

        public string? ClusterId { get; set; }

        public string? ContentEncoding { get; set; }

        public string? ContentType { get; set; }

        public string? CorrelationId { get; set; }

        public byte DeliveryMode { get; set; }

        public string? Expiration { get; set; }

        public IDictionary<string, object>? Headers { get; set; }

        public string? MessageId { get; set; }

        public bool Persistent { get; set; }

        public byte Priority { get; set; }

        public string? ReplyTo { get; set; }

        public PublicationAddress? ReplyToAddress { get; set; }

        public AmqpTimestamp Timestamp { get; set; }

        public string? Type { get; set; }

        public string? UserId { get; set; }

        public void ClearAppId()
        {
            AppId = null;
        }

        public void ClearClusterId()
        {
            ClusterId = null;
        }

        public void ClearContentEncoding()
        {
            ContentEncoding = null;
        }

        public void ClearContentType()
        {
            ContentType = null;
        }

        public void ClearCorrelationId()
        {
            CorrelationId = null;
        }

        public void ClearDeliveryMode()
        {
            DeliveryMode = 0;
        }

        public void ClearExpiration()
        {
            Expiration = null;
        }

        public void ClearHeaders()
        {
            Headers = null;
        }

        public void ClearMessageId()
        {
            MessageId = null;
        }

        public void ClearPriority()
        {
            Priority = 0;
        }

        public void ClearReplyTo()
        {
            ReplyTo = null;
        }

        public void ClearTimestamp()
        {
            Timestamp = default;
        }

        public void ClearType()
        {
            Type = null;
        }

        public void ClearUserId()
        {
            UserId = null;
        }

        public bool IsAppIdPresent() => !string.IsNullOrEmpty(AppId);

        public bool IsClusterIdPresent() => !string.IsNullOrEmpty(ClusterId);

        public bool IsContentEncodingPresent() => !string.IsNullOrEmpty(ContentEncoding);

        public bool IsContentTypePresent() => !string.IsNullOrEmpty(ContentType);

        public bool IsCorrelationIdPresent() => !string.IsNullOrEmpty(CorrelationId);

        public bool IsDeliveryModePresent() => true;

        public bool IsExpirationPresent() => !string.IsNullOrEmpty(Expiration);

        public bool IsHeadersPresent() => Headers is { Count: > 0 };

        public bool IsMessageIdPresent() => !string.IsNullOrEmpty(MessageId);

        public bool IsPriorityPresent() => Priority > 0;

        public bool IsReplyToPresent() => !string.IsNullOrEmpty(ReplyTo);

        public bool IsTimestampPresent() => Timestamp.UnixTime != 0;

        public bool IsTypePresent() => !string.IsNullOrEmpty(Type);

        public bool IsUserIdPresent() => !string.IsNullOrEmpty(UserId);
    }
}
