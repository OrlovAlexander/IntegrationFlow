using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq;

public sealed class RabbitMqBasicPropertiesMapperTests
{
    [Fact]
    public void ApplyDeliveryProperties_SetsPriorityAndExpiration()
    {
        var properties = new TestBasicProperties();

        RabbitMqBasicPropertiesMapper.ApplyDeliveryProperties(
            properties,
            contentType: "application/json",
            persistent: true,
            priority: 7,
            expirationMilliseconds: 60_000);

        Assert.Equal("application/json", properties.ContentType);
        Assert.Equal((byte)2, properties.DeliveryMode);
        Assert.Equal((byte)7, properties.Priority);
        Assert.Equal("60000", properties.Expiration);
    }

    [Fact]
    public void ApplyDeliveryProperties_OmitsOptionalFieldsWhenNull()
    {
        var properties = new TestBasicProperties();

        RabbitMqBasicPropertiesMapper.ApplyDeliveryProperties(
            properties,
            contentType: "text/plain",
            persistent: false,
            priority: null,
            expirationMilliseconds: null);

        Assert.Equal("text/plain", properties.ContentType);
        Assert.Equal((byte)1, properties.DeliveryMode);
        Assert.Equal((byte)0, properties.Priority);
        Assert.Null(properties.Expiration);
    }

    [Fact]
    public void ValidateMessageDeliveryOptions_RejectsNonPositiveExpiration()
    {
        var exception = Assert.Throws<System.InvalidOperationException>(
            () => RabbitMqBasicPropertiesMapper.ValidateMessageDeliveryOptions(0));

        Assert.Contains("ExpirationMilliseconds", exception.Message);
    }

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

        public void ClearAppId() => AppId = null;

        public void ClearClusterId() => ClusterId = null;

        public void ClearContentEncoding() => ContentEncoding = null;

        public void ClearContentType() => ContentType = null;

        public void ClearCorrelationId() => CorrelationId = null;

        public void ClearDeliveryMode() => DeliveryMode = 0;

        public void ClearExpiration() => Expiration = null;

        public void ClearHeaders() => Headers = null;

        public void ClearMessageId() => MessageId = null;

        public void ClearPriority() => Priority = 0;

        public void ClearReplyTo() => ReplyTo = null;

        public void ClearTimestamp() => Timestamp = default;

        public void ClearType() => Type = null;

        public void ClearUserId() => UserId = null;

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
