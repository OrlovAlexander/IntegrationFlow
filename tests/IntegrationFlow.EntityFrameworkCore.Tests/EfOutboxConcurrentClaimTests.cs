using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using Xunit;

namespace IntegrationFlow.EntityFrameworkCore.Tests;

public sealed class EfOutboxConcurrentClaimTests
{
    [Fact]
    public async Task ClaimPendingAsync_ParallelWorkers_ClaimDistinctMessages()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-outbox-concurrent-{Guid.NewGuid():N}");
        var store = new EfOutboxStore<TestIntegrationDbContext>(factory);

        for (var index = 0; index < 10; index++)
        {
            await store.EnqueueAsync(new OutboxMessage(
                Guid.NewGuid(),
                "OrdersOut",
                new byte[] { (byte)index },
                "application/json",
                DateTimeOffset.UtcNow.AddMilliseconds(index),
                0));
        }

        var workerOneTask = store.ClaimPendingAsync(10, "worker-1", TimeSpan.FromMinutes(1));
        var workerTwoTask = store.ClaimPendingAsync(10, "worker-2", TimeSpan.FromMinutes(1));
        await Task.WhenAll(workerOneTask, workerTwoTask);

        var workerOneClaims = workerOneTask.Result;
        var workerTwoClaims = workerTwoTask.Result;

        var allClaimedIds = workerOneClaims
            .Select(message => message.Id)
            .Concat(workerTwoClaims.Select(message => message.Id))
            .ToList();

        Assert.Equal(10, allClaimedIds.Count);
        Assert.Equal(10, allClaimedIds.Distinct().Count());
    }
}
