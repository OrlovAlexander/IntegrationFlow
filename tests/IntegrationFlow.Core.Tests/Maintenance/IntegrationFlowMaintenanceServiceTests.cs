using IntegrationFlow.Contexts.Integrations._00Samples.RpcPending;
using IntegrationFlow.Contexts.Integrations._03Domain.Maintenance;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Xunit;

namespace IntegrationFlow.Core.Tests.Maintenance;

public sealed class IntegrationFlowMaintenanceServiceTests
{
    [Fact]
    public async Task RunCleanupAsync_PurgesOldTerminalPending()
    {
        var pendingStore = new InMemoryRpcPendingStore();
        var completedId = Guid.NewGuid();
        await pendingStore.EnqueueAsync(new RpcPendingRequest(
            completedId,
            "OrdersRpcAsync",
            new byte[] { 1 },
            "application/json",
            DateTimeOffset.UtcNow.AddDays(-40),
            attemptCount: 0,
            RpcPendingStatus.Completed,
            responsePayload: new byte[] { 2 },
            lockedBy: null,
            lockedUntil: null,
            retryAfter: null,
            completedAt: DateTimeOffset.UtcNow.AddDays(-40),
            lastError: null));

        var service = new IntegrationFlowMaintenanceService(
            new IntegrationFlowMaintenanceOptions { RpcPendingTerminalRetention = TimeSpan.FromDays(30) },
            pendingStore);

        await service.RunCleanupAsync();

        Assert.Null(await pendingStore.GetByIdAsync(completedId));
    }
}
