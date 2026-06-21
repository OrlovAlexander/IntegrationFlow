using System.Text;
using System.Text.Json;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Transmitters;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.SentAndForgot;

public sealed class RabbitMqPublishTransmitterTests
{
    [Fact]
    public void SerializeBody_ReturnsBytesForSupportedTypes()
    {
        Assert.Equal(Array.Empty<byte>(), RabbitMqPublishTransmitter.SerializeBody(null!));
        Assert.Equal(new byte[] { 1, 2, 3 }, RabbitMqPublishTransmitter.SerializeBody(new byte[] { 1, 2, 3 }));
        Assert.Equal(Encoding.UTF8.GetBytes("hello"), RabbitMqPublishTransmitter.SerializeBody("hello"));

        var payload = new { Id = 42, Name = "order" };
        var expected = JsonSerializer.SerializeToUtf8Bytes(payload);

        Assert.Equal(expected, RabbitMqPublishTransmitter.SerializeBody(payload));
    }
}
