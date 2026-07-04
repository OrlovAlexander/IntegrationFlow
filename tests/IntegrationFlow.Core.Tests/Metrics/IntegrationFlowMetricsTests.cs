using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Processors;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using Xunit;

namespace IntegrationFlow.Core.Tests.Metrics;

public sealed class IntegrationFlowMetricsTests
{
    [Fact]
    public async Task ProcessorBase_RecordsSuccessfulProcessing()
    {
        var metrics = new TestMetrics();
        var logger = NullIntegrationLogger.Instance;
        var publisher = PublisherBase.Create<RabbitMqPublisher>(
            logger,
            new MetricsTestRabbitMqPublisherSide(Guid.NewGuid().ToString("N")));
        publisher.Metrics = metrics;

        var configuration = publisher.IntegrationPublisherSide.GetConfiguration(publisher, logger);
        var processor = ProcessorBase.Create<RabbitMqProcessor, MetricsProcessorSide>(
            publisher,
            configuration,
            logger,
            Guid.NewGuid().ToString("N"));

        await processor.ProcessMessageAsync(
            new RabbitMqReceivedMessage(Array.Empty<byte>(), 1, "q", "m1", null));

        Assert.Equal(1, metrics.ProcessedCount);
        Assert.Equal("Inbox", metrics.LastProfileName);
        Assert.True(metrics.LastSuccess);
    }

    [Fact]
    public async Task OutboxRelayService_RecordsPendingCountOnEmptyBatch()
    {
        var store = new MetricsOutboxStore();
        var metrics = new TestMetrics();
        var service = new OutboxRelayService(
            store,
            NullIntegrationLogger.Instance,
            new OutboxRelayOptions(),
            metrics);

        await service.RelayBatchAsync();

        Assert.Equal(0, metrics.PublishedCount);
        Assert.Equal(0, metrics.FailedCount);
        Assert.Equal(0, metrics.AbandonedCount);
        Assert.Equal(0, metrics.LastPendingCount);
    }

    /// <summary>
    /// Isolated publisher side: loads the Inbox profile but uses a unique TypeCollection cache key
    /// so parallel tests do not share <see cref="PublisherBase.Metrics"/>.
    /// </summary>
    private sealed class MetricsTestRabbitMqPublisherSide : RabbitMqIntegrationPublisherSideBase
    {
        private readonly string cacheKeySuffix;

        public MetricsTestRabbitMqPublisherSide(string cacheKeySuffix)
        {
            if (string.IsNullOrWhiteSpace(cacheKeySuffix))
            {
                throw new ArgumentException("Cache key suffix is required.", nameof(cacheKeySuffix));
            }

            this.cacheKeySuffix = cacheKeySuffix;
        }

        protected override string ConfigurationName => "Inbox";

        public override string GetPublisherCacheKey()
            => $"{base.GetPublisherCacheKey()}|{cacheKeySuffix}";
    }

    private sealed class MetricsProcessorSide : IntegrationProcessorSideBase
    {
        public override Contexts.Integrations._03Domain.ReceiveAndProcess.Validator.IValidator GetValidator(
            PublisherBase publisher,
            Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg.IConfiguration configuration,
            Contexts.Integrations._03Domain.IIntegrationLogger logger)
            => null!;

        public override Contexts.Integrations._03Domain.ReceiveAndProcess.Logging.ILogging GetLogging(
            PublisherBase publisher,
            Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg.IConfiguration configuration,
            Contexts.Integrations._03Domain.IIntegrationLogger logger)
            => null!;

        public override Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing.IInboxMessageFailedProcessing GetInboxMessageFailedProcessing(
            PublisherBase publisher,
            Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg.IConfiguration configuration,
            Contexts.Integrations._03Domain.IIntegrationLogger logger)
            => null!;

        public override Contexts.Integrations._03Domain.ReceiveAndProcess.Formatter.IFormatterInboxMessage GetFormatterInboxMessage(
            PublisherBase publisher,
            Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg.IConfiguration configuration,
            Contexts.Integrations._03Domain.IIntegrationLogger logger)
            => null!;

        public override IInboxMessageProcessing GetInboxMessageProcessing(
            PublisherBase publisher,
            Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg.IConfiguration configuration,
            Contexts.Integrations._03Domain.IIntegrationLogger logger)
            => new DelegateInboxMessageProcessing(_ => { });
    }

    private sealed class TestMetrics : IIntegrationFlowMetrics
    {
        public int ProcessedCount { get; private set; }

        public int PublishedCount { get; private set; }

        public int FailedCount { get; private set; }

        public int AbandonedCount { get; private set; }

        public int LastPendingCount { get; private set; }

        public string? LastProfileName { get; private set; }

        public bool LastSuccess { get; private set; }

        public void RecordMessageProcessed(string profileName, TimeSpan duration, bool success)
        {
            ProcessedCount++;
            LastProfileName = profileName;
            LastSuccess = success;
        }

        public void RecordOutboxRelayPublished(int count) => PublishedCount += count;

        public void RecordOutboxRelayFailed(int count) => FailedCount += count;

        public void RecordOutboxRelayAbandoned(int count) => AbandonedCount += count;

        public void RecordOutboxPending(int count) => LastPendingCount = count;

        public int RequestReplyCount { get; private set; }

        public bool LastRequestReplySuccess { get; private set; }

        public bool LastRequestReplyTimedOut { get; private set; }

        public void RecordRequestReply(string profileName, TimeSpan duration, bool success, bool timedOut = false)
        {
            RequestReplyCount++;
            LastProfileName = profileName;
            LastRequestReplySuccess = success;
            LastRequestReplyTimedOut = timedOut;
        }
    }

    private sealed class MetricsOutboxStore : IOutboxStore
    {
        private readonly List<OutboxMessage> pending = new();

        public Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            pending.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
            int batchSize,
            string workerId,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default)
        {
            var claimed = pending.Count == 0
                ? Array.Empty<OutboxMessage>()
                : new[] { pending[0] };
            if (pending.Count > 0)
            {
                pending.RemoveAt(0);
            }

            return Task.FromResult((IReadOnlyList<OutboxMessage>)claimed);
        }

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
            => Task.FromResult((IReadOnlyList<OutboxMessage>)pending.ToArray());

        public Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> ReplayAbandonedAsync(
            Guid id,
            bool resetAttemptCount = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
