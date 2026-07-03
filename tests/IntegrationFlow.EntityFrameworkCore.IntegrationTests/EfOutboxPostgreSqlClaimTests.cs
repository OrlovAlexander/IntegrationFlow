using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using IntegrationFlow.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationFlow.EntityFrameworkCore.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class EfOutboxPostgreSqlClaimTests : IAsyncLifetime
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
    public async Task ClaimPendingAsync_ParallelWorkers_ClaimDistinctMessages_PostgreSql()
    {
        if (!dockerAvailable || container == null)
        {
            return;
        }

        await using var factory = await ProviderTestDbContextFactory.CreatePostgreSqlAsync(container);
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

        var allClaimedIds = workerOneTask.Result
            .Select(message => message.Id)
            .Concat(workerTwoTask.Result.Select(message => message.Id))
            .ToList();

        Assert.Equal(10, allClaimedIds.Count);
        Assert.Equal(10, allClaimedIds.Distinct().Count());
    }
}
