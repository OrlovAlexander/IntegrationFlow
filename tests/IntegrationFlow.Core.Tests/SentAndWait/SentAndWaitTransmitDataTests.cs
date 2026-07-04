using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using Xunit;

namespace IntegrationFlow.Tests.SentAndWait;

public sealed class SentAndWaitTransmitDataTests
{
    [Fact]
    public void WithMessageId_PreservesDataAndSetsMessageId()
    {
        var data = new TransmitData("payload", "order-42");

        Assert.Equal("payload", data.Data);
        Assert.Equal("order-42", data.MessageId);

        var updated = data.WithMessageId("order-99");

        Assert.Equal("payload", updated.Data);
        Assert.Equal("order-99", updated.MessageId);
        Assert.Equal("order-42", data.MessageId);
    }
}
