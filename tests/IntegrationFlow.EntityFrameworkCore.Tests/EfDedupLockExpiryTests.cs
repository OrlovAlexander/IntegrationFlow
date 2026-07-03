using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.EntityFrameworkCore.Deduplication;
using IntegrationFlow.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationFlow.EntityFrameworkCore.Tests;

public sealed class EfDedupLockExpiryTests
{
    [Fact]
    public async Task TryBegin_AfterLockExpired_ReturnsAcquired()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-dedup-expiry-{Guid.NewGuid():N}");
        var store = new EfMessageDeduplicationStore<TestIntegrationDbContext>(
            factory,
            new MessageDeduplicationOptions { ProcessingLockDuration = TimeSpan.Zero });

        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-expired"));

        await using (var context = factory.CreateDbContext())
        {
            var entity = await context.ProcessedMessages.FindAsync("msg-expired");
            Assert.NotNull(entity);
            entity!.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
            await context.SaveChangesAsync();
        }

        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-expired"));
    }

    [Fact]
    public async Task TryBegin_BeforeLockExpired_ReturnsInProgress()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-dedup-expiry-{Guid.NewGuid():N}");
        var store = new EfMessageDeduplicationStore<TestIntegrationDbContext>(
            factory,
            new MessageDeduplicationOptions { ProcessingLockDuration = TimeSpan.FromMinutes(15) });

        Assert.Equal(DeduplicationBeginResult.Acquired, await store.TryBeginProcessingAsync("msg-active"));
        Assert.Equal(DeduplicationBeginResult.InProgress, await store.TryBeginProcessingAsync("msg-active"));
    }
}
