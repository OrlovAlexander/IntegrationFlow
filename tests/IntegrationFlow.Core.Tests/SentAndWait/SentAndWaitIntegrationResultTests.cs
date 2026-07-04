using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter;
using Xunit;

namespace IntegrationFlow.Tests.SentAndWait;

public sealed class SentAndWaitIntegrationResultTests
{
    [Fact]
    public void Succeeded_SetsSuccessAndData()
    {
        var data = new ObtainedData("ok");

        var result = SentAndWaitIntegrationResult.Succeeded(data);

        Assert.True(result.Success);
        Assert.False(result.TimedOut);
        Assert.Equal("ok", result.Data.Data);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Failed_SetsFailureReason()
    {
        var result = SentAndWaitIntegrationResult.Failed("transport error");

        Assert.False(result.Success);
        Assert.False(result.TimedOut);
        Assert.Equal("transport error", result.FailureReason);
        Assert.True(result.Data.IsFailed);
    }

    [Fact]
    public void Timeout_SetsTimedOutFlag()
    {
        var result = SentAndWaitIntegrationResult.Timeout("timed out");

        Assert.False(result.Success);
        Assert.True(result.TimedOut);
        Assert.Equal("timed out", result.FailureReason);
    }
}

internal sealed class StubAsyncTransmitter : ITransmitter, ITransmitterAsync
{
    private readonly Func<TransmitData, CancellationToken, Task<ObtainedData>> transmitAsync;

    public StubAsyncTransmitter(Func<TransmitData, CancellationToken, Task<ObtainedData>> transmitAsync)
    {
        this.transmitAsync = transmitAsync;
    }

    public ObtainedData Transmit(TransmitData transmitData)
        => TransmitAsync(transmitData, CancellationToken.None).GetAwaiter().GetResult();

    public Task<ObtainedData> TransmitAsync(TransmitData transmitData, CancellationToken cancellationToken)
        => transmitAsync(transmitData, cancellationToken);
}
