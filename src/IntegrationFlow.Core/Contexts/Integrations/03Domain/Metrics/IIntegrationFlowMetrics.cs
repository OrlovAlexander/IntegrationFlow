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
    void RecordRequestReply(string profileName, TimeSpan duration, bool success, bool timedOut = false);
}
