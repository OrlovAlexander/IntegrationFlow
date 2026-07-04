using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using IntegrationFlow.EntityFrameworkCore.RpcPending;
using IntegrationFlow.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationFlow.EntityFrameworkCore.Tests;

public sealed class OutboxRpcCompensationHandlerTests
{
    [Fact]
    public async Task TryCompensateAsync_StagesOutboxMessage()
    {
        await using var factory = TestDbContextFactoryFactory.Create($"rpc-compensation-{Guid.NewGuid():N}");
        var handler = new OutboxRpcCompensationHandler<TestIntegrationDbContext>(
            factory,
            new OutboxRpcCompensationOptions { OutboxProfileName = "OrdersCompensation" });

        var pending = new RpcPendingRequest(
            Guid.NewGuid(),
            "OrdersRpcAsync",
            new byte[] { 1, 2 },
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount: 3,
            RpcPendingStatus.Failed,
            responsePayload: null,
            lockedBy: null,
            lockedUntil: null,
            retryAfter: null,
            completedAt: null,
            lastError: "failed");

        var compensated = await handler.TryCompensateAsync(pending);
        Assert.True(compensated);

        await using var context = await factory.CreateDbContextAsync();
        var outboxMessage = await context.Set<OutboxMessageEntity>().SingleAsync();
        Assert.Equal("OrdersCompensation", outboxMessage.ProfileName);
        Assert.NotEqual(pending.Id, outboxMessage.Id);
    }
}
