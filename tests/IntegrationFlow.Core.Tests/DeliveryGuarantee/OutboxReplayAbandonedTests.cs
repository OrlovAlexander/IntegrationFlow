using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00Samples.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using Xunit;

namespace IntegrationFlow.Tests.DeliveryGuarantee;

public sealed class OutboxReplayAbandonedTests
{
    [Fact]
    public async Task ReplayAbandonedAsync_WhenFailed_ReturnsTrueAndResetsToPending()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        await store.EnqueueAsync(CreateMessage(id, attemptCount: 3));
        await store.ClaimPendingAsync(1, "worker-1", TimeSpan.FromMinutes(1));
        await store.MarkAbandonedAsync(id, "worker-1", "Max attempts exceeded.");

        var replayed = await store.ReplayAbandonedAsync(id);

        Assert.True(replayed);
        var pending = await store.GetPendingAsync(10);
        Assert.Single(pending);
        Assert.Equal(id, pending[0].Id);
        Assert.Equal(OutboxMessageStatus.Pending, pending[0].Status);
        Assert.Equal(3, pending[0].AttemptCount);
        Assert.Null(pending[0].LastError);
        Assert.Null(pending[0].RetryAfter);
    }

    [Fact]
    public async Task ReplayAbandonedAsync_WhenFailedWithResetAttemptCount_ResetsAttempts()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        await store.EnqueueAsync(CreateMessage(id, attemptCount: 5));
        await store.ClaimPendingAsync(1, "worker-1", TimeSpan.FromMinutes(1));
        await store.MarkAbandonedAsync(id, "worker-1", "Max attempts exceeded.");

        var replayed = await store.ReplayAbandonedAsync(id, resetAttemptCount: true);

        Assert.True(replayed);
        var pending = await store.GetPendingAsync(10);
        Assert.Single(pending);
        Assert.Equal(0, pending[0].AttemptCount);
    }

    [Fact]
    public async Task ReplayAbandonedAsync_WhenPending_ReturnsFalse()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        await store.EnqueueAsync(CreateMessage(id));

        var replayed = await store.ReplayAbandonedAsync(id);

        Assert.False(replayed);
    }

    [Fact]
    public async Task ReplayAbandonedAsync_WhenNotFound_ReturnsFalse()
    {
        var store = new InMemoryOutboxStore();

        var replayed = await store.ReplayAbandonedAsync(Guid.NewGuid());

        Assert.False(replayed);
    }

    [Fact]
    public async Task ReplayAbandonedAsync_WhenPublished_ReturnsFalse()
    {
        var store = new InMemoryOutboxStore();
        var id = Guid.NewGuid();
        await store.EnqueueAsync(CreateMessage(id));
        await store.ClaimPendingAsync(1, "worker-1", TimeSpan.FromMinutes(1));
        await store.MarkPublishedAsync(id, "worker-1");

        var replayed = await store.ReplayAbandonedAsync(id);

        Assert.False(replayed);
    }

    private static OutboxMessage CreateMessage(Guid id, int attemptCount = 0)
        => new(
            id,
            "OrdersOut",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount);
}
