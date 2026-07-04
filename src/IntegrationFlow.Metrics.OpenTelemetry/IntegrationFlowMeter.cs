using System.Diagnostics.Metrics;

namespace IntegrationFlow.Metrics.OpenTelemetry;

internal sealed class IntegrationFlowMeter : IDisposable
{
    private readonly Meter meter;
    private long pendingCount;

    public IntegrationFlowMeter(string meterName)
    {
        meter = new Meter(meterName);
        MessagesProcessed = meter.CreateCounter<long>(
            "integrationflow.message.processed",
            description: "Inbox messages processed by IntegrationFlow consumer.");
        MessageProcessingDuration = meter.CreateHistogram<double>(
            "integrationflow.message.processing.duration",
            unit: "s",
            description: "Inbox message processing duration in seconds.");
        OutboxRelayPublished = meter.CreateCounter<long>(
            "integrationflow.outbox.relay.published",
            description: "Outbox messages successfully relayed to the broker.");
        OutboxRelayFailed = meter.CreateCounter<long>(
            "integrationflow.outbox.relay.failed",
            description: "Outbox relay publish failures.");
        OutboxRelayAbandoned = meter.CreateCounter<long>(
            "integrationflow.outbox.relay.abandoned",
            description: "Outbox messages abandoned after max relay attempts.");
        meter.CreateObservableGauge(
            "integrationflow.outbox.pending",
            ObservePendingCount,
            description: "Current pending outbox message count.");
        RequestReplyCompleted = meter.CreateCounter<long>(
            "integrationflow.requestreply.completed",
            description: "SentAndWait request-reply operations completed.");
        RequestReplyDuration = meter.CreateHistogram<double>(
            "integrationflow.requestreply.duration",
            unit: "s",
            description: "SentAndWait request-reply round-trip duration in seconds.");
        RequestReplyRetryAfterTimeout = meter.CreateCounter<long>(
            "integrationflow.requestreply.retry_after_timeout",
            description: "SentAndWait request-reply retries after timeout.");
    }

    public Counter<long> MessagesProcessed { get; }

    public Histogram<double> MessageProcessingDuration { get; }

    public Counter<long> OutboxRelayPublished { get; }

    public Counter<long> OutboxRelayFailed { get; }

    public Counter<long> OutboxRelayAbandoned { get; }

    public Counter<long> RequestReplyCompleted { get; }

    public Histogram<double> RequestReplyDuration { get; }

    public Counter<long> RequestReplyRetryAfterTimeout { get; }

    public void SetPendingCount(int count)
    {
        Interlocked.Exchange(ref pendingCount, count);
    }

    public void Dispose()
    {
        meter.Dispose();
    }

    private Measurement<long> ObservePendingCount()
        => new(Interlocked.Read(ref pendingCount));
}
