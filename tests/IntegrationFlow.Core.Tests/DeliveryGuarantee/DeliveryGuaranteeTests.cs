using System;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00Samples.Outbox;
using IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;
using Xunit;

namespace IntegrationFlow.Tests.DeliveryGuarantee;

public sealed class InMemoryOutboxStoreTests
{
    [Fact]
    public async Task EnqueueAndGetPending_ReturnsMessage()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        var message = new OutboxMessage(id, "OrdersOut", new byte[] { 1 }, "application/json", DateTimeOffset.UtcNow, 0);

        await store.EnqueueAsync(message);
        var pending = await store.GetPendingAsync(10);

        Assert.Single(pending);
        Assert.Equal(id, pending[0].Id);
    }

    [Fact]
    public async Task MarkPublished_RemovesFromPending()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        await store.EnqueueAsync(new OutboxMessage(id, "OrdersOut", new byte[] { 1 }, "application/json", DateTimeOffset.UtcNow, 0));

        await store.MarkPublishedAsync(id);
        var pending = await store.GetPendingAsync(10);

        Assert.Empty(pending);
    }
}

public sealed class InMemoryMessageDeduplicationStoreTests
{
    [Fact]
    public async Task TryBeginProcessingAsync_ReturnsFalseForDuplicate()
    {
        var store = new InMemoryMessageDeduplicationStore();

        Assert.True(await store.TryBeginProcessingAsync("msg-1"));
        await store.MarkProcessedAsync("msg-1");
        Assert.False(await store.TryBeginProcessingAsync("msg-1"));
    }
}

public sealed class OutboxTransmitterTests
{
    [Fact]
    public async Task TransmitWithResult_EnqueuesMessage()
    {
        var store = new InMemoryOutboxStore();
        var transmitter = new OutboxTransmitter(store, "OrdersOut");
        var result = transmitter.TransmitWithResult(new TransmitData("payload"));

        Assert.False(string.IsNullOrWhiteSpace(result.MessageId));
        var pending = await store.GetPendingAsync(10);
        Assert.Single(pending);
        Assert.Equal("OrdersOut", pending[0].ProfileName);
    }
}
