using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using IntegrationFlow.EntityFrameworkCore.RpcPending;
using IntegrationFlow.Testing;
using Xunit;

namespace IntegrationFlow.EntityFrameworkCore.Tests;

public sealed class EfRpcPendingStoreTests
{
    [Fact]
    public async Task StageAndComplete_AsyncRpcRoundTrip()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-rpc-pending-{Guid.NewGuid():N}");
        var store = new EfRpcPendingStore<TestIntegrationDbContext>(factory);
        var id = Guid.NewGuid();

        await store.EnqueueAsync(new RpcPendingRequest(
            id,
            "OrdersRpc",
            new byte[] { 1, 2 },
            "application/json",
            DateTimeOffset.UtcNow));

        var claimed = await store.ClaimPendingAsync(10, "worker-1", TimeSpan.FromMinutes(1));
        Assert.Single(claimed);
        Assert.Equal(RpcPendingStatus.InFlight, claimed[0].Status);

        await store.MarkAwaitingResponseAsync(id, "worker-1");
        await store.CompleteAsync(id, new byte[] { 9 });

        var completed = await store.GetByIdAsync(id);
        Assert.NotNull(completed);
        Assert.Equal(RpcPendingStatus.Completed, completed!.Status);
        Assert.Equal(new byte[] { 9 }, completed.ResponsePayload);
    }

    [Fact]
    public async Task WaitForCompletionAsync_ReturnsCompletedRequest()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-rpc-wait-{Guid.NewGuid():N}");
        var store = new EfRpcPendingStore<TestIntegrationDbContext>(factory);
        var id = Guid.NewGuid();

        await store.EnqueueAsync(new RpcPendingRequest(
            id,
            "OrdersRpc",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow));

        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            await store.CompleteAsync(id, new byte[] { 42 });
        });

        var result = await store.WaitForCompletionAsync(id, TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
        Assert.Equal(RpcPendingStatus.Completed, result!.Status);
    }
}
