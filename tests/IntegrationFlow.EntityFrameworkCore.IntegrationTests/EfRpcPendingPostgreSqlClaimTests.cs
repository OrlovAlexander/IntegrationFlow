using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using IntegrationFlow.EntityFrameworkCore.RpcPending;
using IntegrationFlow.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationFlow.EntityFrameworkCore.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class EfRpcPendingPostgreSqlClaimTests : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private bool dockerAvailable;

    public async Task InitializeAsync()
    {
        dockerAvailable = await DockerAvailability.IsAvailableAsync();
        if (!dockerAvailable)
        {
            return;
        }

        container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (container != null)
        {
            await container.DisposeAsync();
        }
    }

    [Fact]
    public async Task ClaimPendingAsync_ParallelWorkers_ClaimDistinctRequests_PostgreSql()
    {
        if (!dockerAvailable || container == null)
        {
            return;
        }

        await using var factory = await ProviderTestDbContextFactory.CreatePostgreSqlAsync(container);
        var store = new EfRpcPendingStore<TestIntegrationDbContext>(factory);

        for (var index = 0; index < 10; index++)
        {
            await store.EnqueueAsync(new RpcPendingRequest(
                Guid.NewGuid(),
                "OrdersRpcAsync",
                new byte[] { (byte)index },
                "application/json",
                DateTimeOffset.UtcNow.AddMilliseconds(index)));
        }

        var workerOneTask = store.ClaimPendingAsync(10, "worker-1", TimeSpan.FromMinutes(1));
        var workerTwoTask = store.ClaimPendingAsync(10, "worker-2", TimeSpan.FromMinutes(1));
        await Task.WhenAll(workerOneTask, workerTwoTask);

        var allClaimedIds = workerOneTask.Result
            .Select(request => request.Id)
            .Concat(workerTwoTask.Result.Select(request => request.Id))
            .ToList();

        Assert.Equal(10, allClaimedIds.Count);
        Assert.Equal(10, allClaimedIds.Distinct().Count());
    }
}
