using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Metrics;

/// <summary>
/// Optional observability hooks for IntegrationFlow operations.
/// </summary>
public interface IIntegrationFlowMetrics
{
    /// <summary>
    /// Records inbox message processing duration and outcome.
    /// </summary>
    /// <param name="profileName">Integration profile or side identifier.</param>
    /// <param name="duration">Processing duration.</param>
    /// <param name="success">True when business handler completed without exception.</param>
    void RecordMessageProcessed(string profileName, TimeSpan duration, bool success);

    /// <summary>
    /// Records successfully relayed outbox messages in a batch.
    /// </summary>
    void RecordOutboxRelayPublished(int count);

    /// <summary>
    /// Records outbox relay publish failures in a batch.
    /// </summary>
    void RecordOutboxRelayFailed(int count);

    /// <summary>
    /// Records outbox messages abandoned after max attempts.
    /// </summary>
    void RecordOutboxRelayAbandoned(int count);

    /// <summary>
    /// Records current pending outbox message count.
    /// </summary>
    void RecordOutboxPending(int count);

    /// <summary>
    /// Records request-reply (SentAndWait) operation duration and outcome.
    /// </summary>
    /// <param name="profileName">Integration profile or side identifier.</param>
    /// <param name="duration">Round-trip duration.</param>
    /// <param name="success">True when a valid response was received.</param>
    /// <param name="timedOut">True when the operation ended due to response timeout.</param>
    /// <param name="transport">Optional transport tag, e.g. <c>rest</c> or <c>rabbitmq</c>.</param>
    void RecordRequestReply(
        string profileName,
        TimeSpan duration,
        bool success,
        bool timedOut = false,
        string? transport = null);

    /// <summary>
    /// Records a request-reply retry after timeout (same MessageId, new CorrelationId).
    /// </summary>
    void RecordRequestReplyRetryAfterTimeout(string profileName);

    /// <summary>
    /// Records successfully relayed async RPC pending requests in a batch.
    /// </summary>
    void RecordRpcPendingRelayPublished(int count);

    /// <summary>
    /// Records async RPC pending relay publish failures in a batch.
    /// </summary>
    void RecordRpcPendingRelayFailed(int count);

    /// <summary>
    /// Records async RPC pending requests abandoned after max attempts.
    /// </summary>
    void RecordRpcPendingRelayAbandoned(int count);

    /// <summary>
    /// Records current awaiting-response async RPC pending count.
    /// </summary>
    void RecordRpcPendingAwaiting(int count);

    /// <summary>
    /// Records async RPC pending round-trip duration and outcome.
    /// </summary>
    void RecordRpcPendingCompleted(string profileName, TimeSpan duration, bool success, bool timedOut = false);

    /// <summary>
    /// Records RabbitMQ listener reconnect after connection loss.
    /// </summary>
    void RecordListenerReconnect(string profileName);

    /// <summary>
    /// Records messages nack-requeued during listener shutdown.
    /// </summary>
    void RecordListenerShutdownRequeue(string profileName);

    /// <summary>
    /// Records RabbitMQ consumer acknowledgement outcomes.
    /// </summary>
    /// <param name="profileName">Integration profile or side identifier.</param>
    /// <param name="reason">Outcome reason: <see cref="ConsumerOutcomeReason"/>.</param>
    void RecordConsumerOutcome(string profileName, string reason);

    /// <summary>
    /// Records current RabbitMQ connection pool size.
    /// </summary>
    /// <param name="kind">Pool kind: <c>rpc</c> or <c>publish</c>.</param>
    /// <param name="size">Number of pooled connections.</param>
    void RecordConnectionPoolSize(string kind, int size);

    /// <summary>
    /// Records RabbitMQ broker connectivity for a transport endpoint.
    /// </summary>
    /// <param name="profileName">Integration profile name.</param>
    /// <param name="kind">Transport kind: <c>listener</c>, <c>outbox_relay</c>, <c>rpc_correlation</c>.</param>
    /// <param name="connected"><c>true</c> when broker session is active.</param>
    void RecordBrokerConnected(string profileName, string kind, bool connected);
}
