using IntegrationFlow.Contexts.Integrations._00Samples.RpcPending;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Xunit;

namespace IntegrationFlow.Core.Tests.RpcPending;

public sealed class RpcPendingCompensationServiceTests
{
    [Fact]
    public async Task ProcessBatchAsync_MarksCompensatedWhenHandlerSucceeds()
    {
        var store = new InMemoryRpcPendingStore();
        var pendingId = Guid.NewGuid();
        await store.EnqueueAsync(new RpcPendingRequest(
            pendingId,
            "OrdersRpcAsync",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount: 3,
            RpcPendingStatus.Failed,
            responsePayload: null,
            lockedBy: null,
            lockedUntil: null,
            retryAfter: null,
            completedAt: null,
            lastError: "Max attempts exceeded."));

        var service = new RpcPendingCompensationService(
            store,
            new[] { new TestCompensationHandler(success: true) },
            NullIntegrationLogger.Instance);

        await service.ProcessBatchAsync();

        var updated = await store.GetByIdAsync(pendingId);
        Assert.NotNull(updated);
        Assert.NotNull(updated!.CompensatedAt);
    }

    [Fact]
    public async Task ProcessBatchAsync_DoesNothingWithoutHandlers()
    {
        var store = new InMemoryRpcPendingStore();
        var pendingId = Guid.NewGuid();
        await store.EnqueueAsync(new RpcPendingRequest(
            pendingId,
            "OrdersRpcAsync",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount: 0,
            RpcPendingStatus.TimedOut,
            responsePayload: null,
            lockedBy: null,
            lockedUntil: null,
            retryAfter: null,
            completedAt: null,
            lastError: "Pending response timeout."));

        var service = new RpcPendingCompensationService(
            store,
            Array.Empty<IRpcCompensationHandler>(),
            NullIntegrationLogger.Instance);

        await service.ProcessBatchAsync();

        var updated = await store.GetByIdAsync(pendingId);
        Assert.NotNull(updated);
        Assert.Null(updated!.CompensatedAt);
    }

    private sealed class TestCompensationHandler : IRpcCompensationHandler
    {
        private readonly bool success;

        public TestCompensationHandler(bool success)
        {
            this.success = success;
        }

        public Task<bool> TryCompensateAsync(RpcPendingRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(success);
    }
}
