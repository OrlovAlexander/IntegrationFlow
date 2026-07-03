using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using Xunit;

namespace IntegrationFlow.Tests.DeliveryGuarantee;

public sealed class OutboxRelayServiceTests
{
    [Fact]
    public void CalculateRetryDelay_UsesLinearBackoffWhenDisabled()
    {
        var service = new OutboxRelayService(
            new InMemoryOutboxStoreStub(),
            NullIntegrationLogger.Instance,
            new OutboxRelayOptions
            {
                UseExponentialBackoff = false,
                RetryBackoffBase = TimeSpan.FromSeconds(5)
            });

        Assert.Equal(TimeSpan.FromSeconds(10), service.CalculateRetryDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(15), service.CalculateRetryDelay(2));
    }

    [Fact]
    public void CalculateRetryDelay_UsesExponentialBackoffWithCap()
    {
        var service = new OutboxRelayService(
            new InMemoryOutboxStoreStub(),
            NullIntegrationLogger.Instance,
            new OutboxRelayOptions
            {
                UseExponentialBackoff = true,
                RetryBackoffBase = TimeSpan.FromSeconds(5),
                BackoffMultiplier = 2.0,
                MaxRetryDelay = TimeSpan.FromMinutes(1)
            });

        Assert.Equal(TimeSpan.FromSeconds(5), service.CalculateRetryDelay(0));
        Assert.Equal(TimeSpan.FromSeconds(10), service.CalculateRetryDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(20), service.CalculateRetryDelay(2));
        Assert.Equal(TimeSpan.FromMinutes(1), service.CalculateRetryDelay(10));
    }

    private sealed class InMemoryOutboxStoreStub : IOutboxStore
    {
        public Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
            int batchSize,
            string workerId,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OutboxMessage>>(Array.Empty<OutboxMessage>());

        public Task MarkPublishedAsync(Guid id, string workerId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkFailedAsync(
            Guid id,
            string workerId,
            string error,
            TimeSpan retryAfter,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkAbandonedAsync(Guid id, string workerId, string reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReleaseExpiredClaimsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OutboxMessage>>(Array.Empty<OutboxMessage>());

        public Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
