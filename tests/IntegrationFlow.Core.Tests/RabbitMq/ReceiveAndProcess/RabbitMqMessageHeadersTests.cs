using System.Collections;
using System.Collections.Generic;
using System.Text;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using Xunit;

namespace IntegrationFlow.Core.Tests.RabbitMq.ReceiveAndProcess;

public sealed class RabbitMqMessageHeadersTests
{
    [Fact]
    public void Snapshot_ReturnsEmpty_WhenHeadersNull()
    {
        var snapshot = RabbitMqMessageHeaders.Snapshot(null);

        Assert.Same(RabbitMqMessageHeaders.Empty, snapshot);
        Assert.Empty(snapshot);
    }

    [Fact]
    public void Snapshot_CreatesReadOnlyCopy()
    {
        var source = new Dictionary<string, object> { ["x-tenant-id"] = "acme" };

        var snapshot = RabbitMqMessageHeaders.Snapshot(source);
        source["x-tenant-id"] = "changed";

        Assert.Equal("acme", snapshot["x-tenant-id"]);
    }

    [Fact]
    public void TryGetString_ReadsStringHeader()
    {
        var headers = RabbitMqMessageHeaders.Snapshot(new Dictionary<string, object>
        {
            ["x-tenant-id"] = "acme",
        });

        var found = RabbitMqMessageHeaders.TryGetString(headers, "x-tenant-id", out var value);

        Assert.True(found);
        Assert.Equal("acme", value);
    }

    [Fact]
    public void TryGetString_ReadsByteArrayHeader()
    {
        var headers = RabbitMqMessageHeaders.Snapshot(new Dictionary<string, object>
        {
            [RabbitMqMessageHeaders.TraceParent] = Encoding.UTF8.GetBytes("00-abc-def-01"),
        });

        var found = RabbitMqMessageHeaders.TryGetString(headers, RabbitMqMessageHeaders.TraceParent, out var value);

        Assert.True(found);
        Assert.Equal("00-abc-def-01", value);
    }

    [Fact]
    public void GetDeathCount_ReadsCountFromHeader()
    {
        var headers = RabbitMqMessageHeaders.Snapshot(new Dictionary<string, object>
        {
            [RabbitMqMessageHeaders.Death] = new ArrayList
            {
                new Dictionary<string, object> { ["count"] = 3L },
            },
        });

        Assert.Equal(3, RabbitMqMessageHeaders.GetDeathCount(headers));
    }
}

public sealed class RabbitMqReceivedMessageHeadersTests
{
    [Fact]
    public void Constructor_ExposesHeadersOnMessage()
    {
        var headers = new Dictionary<string, object>
        {
            ["x-tenant-id"] = "acme",
            ["traceparent"] = "00-abc-def-01",
        };

        var message = new RabbitMqReceivedMessage(
            new byte[] { 1, 2 },
            deliveryTag: 5,
            routingKey: "rk",
            messageId: "msg-1",
            correlationId: "corr-1",
            headers: headers);

        Assert.Equal(2, message.Headers.Count);
        Assert.True(RabbitMqMessageHeaders.TryGetString(message.Headers, "x-tenant-id", out var tenantId));
        Assert.Equal("acme", tenantId);
    }

    [Fact]
    public void Constructor_UsesEmptyHeaders_WhenNotProvided()
    {
        var message = new RabbitMqReceivedMessage(
            new byte[] { 1 },
            deliveryTag: 1,
            routingKey: "rk",
            messageId: "msg",
            correlationId: "corr");

        Assert.Same(RabbitMqMessageHeaders.Empty, message.Headers);
    }
}
