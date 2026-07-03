using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00Samples.Outbox;
using IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
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

    [Fact]
    public async Task ClaimPendingAsync_ReservesMessagesForWorker()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        await store.EnqueueAsync(new OutboxMessage(id, "OrdersOut", new byte[] { 1 }, "application/json", DateTimeOffset.UtcNow, 0));

        var workerOneClaims = await store.ClaimPendingAsync(10, "worker-1", TimeSpan.FromMinutes(1));
        var workerTwoClaims = await store.ClaimPendingAsync(10, "worker-2", TimeSpan.FromMinutes(1));

        Assert.Single(workerOneClaims);
        Assert.Empty(workerTwoClaims);
        Assert.Equal(OutboxMessageStatus.InFlight, workerOneClaims[0].Status);
        Assert.Equal("worker-1", workerOneClaims[0].LockedBy);
    }

    [Fact]
    public async Task MarkPublishedAsync_RequiresMatchingWorker()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        await store.EnqueueAsync(new OutboxMessage(id, "OrdersOut", new byte[] { 1 }, "application/json", DateTimeOffset.UtcNow, 0));

        var claimed = await store.ClaimPendingAsync(10, "worker-1", TimeSpan.FromMinutes(1));
        Assert.Single(claimed);

        await store.MarkPublishedAsync(id, "worker-2");
        var reclaim = await store.ClaimPendingAsync(10, "worker-2", TimeSpan.FromMinutes(1));
        Assert.Empty(reclaim);

        await store.MarkPublishedAsync(id, "worker-1");
        var pending = await store.GetPendingAsync(10);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task ReleaseExpiredClaimsAsync_ReleasesStaleInFlightMessage()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        var expiredLock = DateTimeOffset.UtcNow.AddSeconds(-1);
        var message = new OutboxMessage(
            id,
            "OrdersOut",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount: 0,
            OutboxMessageStatus.InFlight,
            lockedBy: "worker-1",
            lockedUntil: expiredLock,
            retryAfter: null,
            lastError: null);

        await store.EnqueueAsync(message);
        await store.ReleaseExpiredClaimsAsync();

        var pending = await store.GetPendingAsync(10);
        Assert.Single(pending);
        Assert.Equal(OutboxMessageStatus.Pending, pending[0].Status);
    }
}

public sealed class InMemoryMessageDeduplicationStoreTests
{
    [Fact]
    public async Task TryBeginProcessingAsync_ReturnsAlreadyProcessedForDuplicate()
    {
        var store = new InMemoryMessageDeduplicationStore();

        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-1"));
        await store.MarkProcessedAsync("msg-1");
        Assert.Equal(DeduplicationBeginResult.AlreadyProcessed, await store.TryBeginProcessingAsync("msg-1"));
    }

    [Fact]
    public async Task ReleaseProcessingAsync_AllowsRetryAfterFailure()
    {
        var store = new InMemoryMessageDeduplicationStore();

        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-1"));
        await store.ReleaseProcessingAsync("msg-1");
        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-1"));
    }

    [Fact]
    public async Task TryBeginProcessingAsync_ReturnsInProgressForParallelDelivery()
    {
        var store = new InMemoryMessageDeduplicationStore();

        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-1"));
        Assert.Equal(DeduplicationBeginResult.InProgress, await store.TryBeginProcessingAsync("msg-1"));
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

    [Fact]
    public void TransmitWithResult_StagesWithoutPersisting()
    {
        var enqueue = new CapturingOutboxEnqueue();
        var transmitter = new OutboxTransmitter(enqueue, "OrdersOut");
        var result = transmitter.TransmitWithResult(new TransmitData("payload"));

        Assert.False(string.IsNullOrWhiteSpace(result.MessageId));
        Assert.Single(enqueue.Staged);
        Assert.Equal("OrdersOut", enqueue.Staged[0].ProfileName);
    }

    private sealed class CapturingOutboxEnqueue : IOutboxEnqueue
    {
        public List<OutboxMessage> Staged { get; } = new();

        public void Stage(OutboxMessage message) => Staged.Add(message);
    }
}

public sealed class SentAndForgotIntegrationOptionsTests
{
    [Fact]
    public void ThrowOnFailure_DefaultsToFalse()
    {
        SentAndForgotIntegrationOptions.ThrowOnFailure = false;
        Assert.False(SentAndForgotIntegrationOptions.ThrowOnFailure);
    }
}
