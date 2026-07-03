using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using IntegrationFlow.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationFlow.EntityFrameworkCore.Tests;

public sealed class EfOutboxReplayAbandonedTests
{
    [Fact]
    public async Task ReplayAbandonedAsync_WhenFailed_ReturnsTrueAndResetsToPending()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-outbox-replay-{Guid.NewGuid():N}");
        var store = new EfOutboxStore<TestIntegrationDbContext>(factory);
        var id = Guid.NewGuid();

        await store.EnqueueAsync(new OutboxMessage(
            id,
            "OrdersOut",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount: 4));

        await store.ClaimPendingAsync(1, "worker-1", TimeSpan.FromMinutes(1));
        await store.MarkAbandonedAsync(id, "worker-1", "Max attempts exceeded.");

        var replayed = await store.ReplayAbandonedAsync(id);

        Assert.True(replayed);

        await using var context = factory.CreateDbContext();
        var entity = await context.OutboxMessages.SingleAsync(message => message.Id == id);
        Assert.Equal(OutboxMessageStatus.Pending, entity.Status);
        Assert.Equal(4, entity.AttemptCount);
        Assert.Null(entity.LastError);
        Assert.Null(entity.RetryAfter);
        Assert.Null(entity.LockedBy);
    }

    [Fact]
    public async Task ReplayAbandonedAsync_WhenFailedWithResetAttemptCount_ResetsAttempts()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-outbox-replay-{Guid.NewGuid():N}");
        var store = new EfOutboxStore<TestIntegrationDbContext>(factory);
        var id = Guid.NewGuid();

        await store.EnqueueAsync(new OutboxMessage(
            id,
            "OrdersOut",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount: 5));

        await store.ClaimPendingAsync(1, "worker-1", TimeSpan.FromMinutes(1));
        await store.MarkAbandonedAsync(id, "worker-1", "Max attempts exceeded.");

        var replayed = await store.ReplayAbandonedAsync(id, resetAttemptCount: true);

        Assert.True(replayed);

        await using var context = factory.CreateDbContext();
        var entity = await context.OutboxMessages.SingleAsync(message => message.Id == id);
        Assert.Equal(0, entity.AttemptCount);
    }

    [Fact]
    public async Task ReplayAbandonedAsync_WhenPending_ReturnsFalse()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-outbox-replay-{Guid.NewGuid():N}");
        var store = new EfOutboxStore<TestIntegrationDbContext>(factory);
        var id = Guid.NewGuid();

        await store.EnqueueAsync(new OutboxMessage(
            id,
            "OrdersOut",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow,
            0));

        var replayed = await store.ReplayAbandonedAsync(id);

        Assert.False(replayed);
    }
}
