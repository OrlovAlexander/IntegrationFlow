using System.Text;
using System.Text.Json;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.SentAndForgot;

public sealed class RabbitMqPublishTransmitterTests
{
    [Fact]
    public void SerializeToBytes_ReturnsBytesForSupportedTypes()
    {
        Assert.Equal(Array.Empty<byte>(), IntegrationPayloadSerializer.SerializeToBytes(null!));
        Assert.Equal(new byte[] { 1, 2, 3 }, IntegrationPayloadSerializer.SerializeToBytes(new byte[] { 1, 2, 3 }));
        Assert.Equal(Encoding.UTF8.GetBytes("hello"), IntegrationPayloadSerializer.SerializeToBytes("hello"));

        var payload = new { Id = 42, Name = "order" };
        var expected = JsonSerializer.SerializeToUtf8Bytes(payload);

        Assert.Equal(expected, IntegrationPayloadSerializer.SerializeToBytes(payload));
    }
}
