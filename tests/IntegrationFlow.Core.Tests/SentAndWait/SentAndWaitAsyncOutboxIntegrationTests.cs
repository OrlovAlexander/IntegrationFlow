using IntegrationFlow.Contexts.Integrations._00Samples.RpcPending;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Validator;
using Xunit;

namespace IntegrationFlow.Core.Tests.SentAndWait;

public sealed class SentAndWaitAsyncOutboxIntegrationTests
{
    [Fact]
    public void CreatePendingRequest_UsesOppositeSideProfileName()
    {
        var store = new InMemoryRpcPendingStore();
        var integration = CreateIntegration(store, profileName: "OrdersRpcAsync");

        var pending = integration.CreatePendingRequest();

        Assert.Equal("OrdersRpcAsync", pending.ProfileName);
        Assert.Equal("""{"orderId":1}""", System.Text.Encoding.UTF8.GetString(pending.RequestPayload));
    }

    [Fact]
    public async Task IntegrateWithResultAsync_ReturnsCompletedResponse()
    {
        var store = new InMemoryRpcPendingStore();
        var integration = CreateIntegration(store, profileName: "OrdersRpcAsync");
        var pending = integration.CreatePendingRequest();
        await store.EnqueueAsync(pending);

        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            await store.CompleteAsync(pending.Id, System.Text.Encoding.UTF8.GetBytes("""{"status":"ok"}"""));
        });

        var result = await integration.IntegrateWithResultAsync(pending.Id, TimeSpan.FromSeconds(5));

        Assert.True(result.Success);
        Assert.Equal("""{"status":"ok"}""", result.Data.Data);
    }

    [Fact]
    public async Task IntegrateWithResultAsync_ReturnsTimeoutWhenResponseMissing()
    {
        var store = new InMemoryRpcPendingStore();
        var integration = CreateIntegration(store, profileName: "OrdersRpcAsync");
        var pending = integration.CreatePendingRequest();
        await store.EnqueueAsync(pending);

        var result = await integration.IntegrateWithResultAsync(pending.Id, TimeSpan.FromMilliseconds(300));

        Assert.False(result.Success);
        Assert.True(result.TimedOut);
    }

    private static SentAndWaitAsyncOutboxIntegration CreateIntegration(
        InMemoryRpcPendingStore store,
        string profileName)
    {
        return new SentAndWaitAsyncOutboxIntegration(
            new TestAsyncOutboxOppositeSide(profileName),
            """{"orderId":1}""",
            store,
            NullIntegrationLogger.Instance);
    }

    private sealed class TestAsyncOutboxOppositeSide : SentAndWaitIntegrationOppositeSide
    {
        private readonly string profileName;

        public TestAsyncOutboxOppositeSide(string profileName)
        {
            this.profileName = profileName;
        }

        public override IFormatterTransmitData GetFormatterSourceData(IIntegrationLogger logger) => null!;

        public override IConfiguration GetTransmitterConfiguration(IIntegrationLogger logger) => null!;

        public override IConnection GetConnection(IConfiguration configuration, IIntegrationLogger logger) => null!;

        public override ITransmitter GetTransmitter(
            IConfiguration configuration,
            IConnection connection,
            IIntegrationLogger logger) => null!;

        public override IValidator GetValidator(IConfiguration configuration, IIntegrationLogger logger) => null!;

        public override IFormatterObtainedData GetFormatterObtainedData(IIntegrationLogger logger) => null!;

        public override ILogging GetLogging(IIntegrationLogger logger) => null!;

        public override string GetRpcPendingProfileName(IIntegrationLogger logger) => profileName;

        protected override object GetIntegrationOppositeSideCode() => profileName;
    }
}
