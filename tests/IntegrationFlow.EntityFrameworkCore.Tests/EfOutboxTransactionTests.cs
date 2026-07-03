using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using IntegrationFlow.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationFlow.EntityFrameworkCore.Tests;

public sealed class EfOutboxTransactionTests
{
    [Fact]
    public async Task EnqueueOutboxMessage_RollsBackWithTransaction()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-outbox-tx-{Guid.NewGuid():N}");
        await using var context = factory.CreateDbContext();
        var id = Guid.NewGuid();

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            context.EnqueueOutboxMessage(new OutboxMessage(
                id,
                "OrdersOut",
                new byte[] { 1 },
                "application/json",
                DateTimeOffset.UtcNow,
                0));

            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        var count = await context.OutboxMessages.CountAsync(message => message.Id == id);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task EnqueueOutboxMessage_CommitsWithTransaction()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-outbox-tx-{Guid.NewGuid():N}");
        await using var context = factory.CreateDbContext();
        var id = Guid.NewGuid();

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            context.EnqueueOutboxMessage(new OutboxMessage(
                id,
                "OrdersOut",
                new byte[] { 1 },
                "application/json",
                DateTimeOffset.UtcNow,
                0));

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var count = await context.OutboxMessages.CountAsync(message => message.Id == id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task EfOutboxEnqueue_Stage_DoesNotSaveUntilSaveChanges()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"ef-outbox-tx-{Guid.NewGuid():N}");
        await using var context = factory.CreateDbContext();
        var enqueue = new EfOutboxEnqueue<TestIntegrationDbContext>(context);
        var id = Guid.NewGuid();

        enqueue.Stage(new OutboxMessage(
            id,
            "OrdersOut",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow,
            0));

        Assert.Equal(0, await context.OutboxMessages.CountAsync());

        await context.SaveChangesAsync();
        Assert.Equal(1, await context.OutboxMessages.CountAsync(message => message.Id == id));
    }
}
