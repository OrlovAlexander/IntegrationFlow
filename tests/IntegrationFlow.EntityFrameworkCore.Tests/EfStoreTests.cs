using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.EntityFrameworkCore.Deduplication;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using IntegrationFlow.Testing;
using Xunit;

namespace IntegrationFlow.EntityFrameworkCore.Tests;

public sealed class EfOutboxStoreTests
{
    [Fact]
    public async Task ClaimPendingAsync_ReservesMessagesForWorker()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-outbox-{Guid.NewGuid():N}");
        var store = new EfOutboxStore<TestIntegrationDbContext>(factory);
        var id = Guid.NewGuid();

        await store.EnqueueAsync(new OutboxMessage(
            id,
            "OrdersOut",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow,
            0));

        var workerOneClaims = await store.ClaimPendingAsync(10, "worker-1", TimeSpan.FromMinutes(1));
        var workerTwoClaims = await store.ClaimPendingAsync(10, "worker-2", TimeSpan.FromMinutes(1));

        Assert.Single(workerOneClaims);
        Assert.Empty(workerTwoClaims);
        Assert.Equal(OutboxMessageStatus.InFlight, workerOneClaims[0].Status);
    }

    [Fact]
    public async Task MarkPublishedAsync_RequiresMatchingWorker()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-outbox-{Guid.NewGuid():N}");
        var store = new EfOutboxStore<TestIntegrationDbContext>(factory);
        var id = Guid.NewGuid();

        await store.EnqueueAsync(new OutboxMessage(
            id,
            "OrdersOut",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow,
            0));

        await store.ClaimPendingAsync(10, "worker-1", TimeSpan.FromMinutes(1));
        await store.MarkPublishedAsync(id, "worker-2");

        var reclaim = await store.ClaimPendingAsync(10, "worker-2", TimeSpan.FromMinutes(1));
        Assert.Empty(reclaim);

        await store.MarkPublishedAsync(id, "worker-1");
        var pending = await store.GetPendingAsync(10);
        Assert.Empty(pending);
    }
}

public sealed class EfMessageDeduplicationStoreTests
{
    [Fact]
    public async Task ReleaseProcessingAsync_AllowsRetryAfterFailure()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-dedup-{Guid.NewGuid():N}");
        var store = new EfMessageDeduplicationStore<TestIntegrationDbContext>(
            factory,
            new MessageDeduplicationOptions { ProcessedRetention = TimeSpan.FromDays(7) });

        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-1"));
        await store.ReleaseProcessingAsync("msg-1");
        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-1"));
    }

    [Fact]
    public async Task MarkProcessedAsync_PreventsDuplicateProcessing()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-dedup-{Guid.NewGuid():N}");
        var store = new EfMessageDeduplicationStore<TestIntegrationDbContext>(
            factory,
            new MessageDeduplicationOptions { ProcessedRetention = TimeSpan.FromDays(7) });

        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-2"));
        await store.MarkProcessedAsync("msg-2");
        Assert.Equal(DeduplicationBeginResult.AlreadyProcessed, await store.TryBeginProcessingAsync("msg-2"));
    }
}
