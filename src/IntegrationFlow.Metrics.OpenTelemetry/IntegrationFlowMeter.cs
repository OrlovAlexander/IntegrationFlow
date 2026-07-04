using System.Diagnostics.Metrics;

namespace IntegrationFlow.Metrics.OpenTelemetry;

internal sealed class IntegrationFlowMeter : IDisposable
{
    private readonly Meter meter;
    private long pendingCount;
    private long rpcPendingAwaitingCount;

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
        RpcPendingRelayPublished = meter.CreateCounter<long>(
            "integrationflow.rpc.pending.relay.published",
            description: "Async RPC pending requests successfully relayed to the broker.");
        RpcPendingRelayFailed = meter.CreateCounter<long>(
            "integrationflow.rpc.pending.relay.failed",
            description: "Async RPC pending relay publish failures.");
        RpcPendingRelayAbandoned = meter.CreateCounter<long>(
            "integrationflow.rpc.pending.relay.abandoned",
            description: "Async RPC pending requests abandoned after max relay attempts.");
        meter.CreateObservableGauge(
            "integrationflow.rpc.pending.awaiting",
            ObserveRpcPendingAwaitingCount,
            description: "Current async RPC pending requests awaiting response.");
        RpcPendingCompleted = meter.CreateCounter<long>(
            "integrationflow.rpc.pending.completed",
            description: "Async RPC pending requests reaching terminal state.");
        RpcPendingDuration = meter.CreateHistogram<double>(
            "integrationflow.rpc.pending.duration",
            unit: "s",
            description: "Async RPC pending round-trip duration in seconds.");
    }

    public Counter<long> MessagesProcessed { get; }

    public Histogram<double> MessageProcessingDuration { get; }

    public Counter<long> OutboxRelayPublished { get; }

    public Counter<long> OutboxRelayFailed { get; }

    public Counter<long> OutboxRelayAbandoned { get; }

    public Counter<long> RequestReplyCompleted { get; }

    public Histogram<double> RequestReplyDuration { get; }

    public Counter<long> RequestReplyRetryAfterTimeout { get; }

    public Counter<long> RpcPendingRelayPublished { get; }

    public Counter<long> RpcPendingRelayFailed { get; }

    public Counter<long> RpcPendingRelayAbandoned { get; }

    public Counter<long> RpcPendingCompleted { get; }

    public Histogram<double> RpcPendingDuration { get; }

    public void SetPendingCount(int count)
    {
        Interlocked.Exchange(ref pendingCount, count);
    }

    public void SetRpcPendingAwaitingCount(int count)
    {
        Interlocked.Exchange(ref rpcPendingAwaitingCount, count);
    }

    public void Dispose()
    {
        meter.Dispose();
    }

    private Measurement<long> ObservePendingCount()
        => new(Interlocked.Read(ref pendingCount));

    private Measurement<long> ObserveRpcPendingAwaitingCount()
        => new(Interlocked.Read(ref rpcPendingAwaitingCount));
}
