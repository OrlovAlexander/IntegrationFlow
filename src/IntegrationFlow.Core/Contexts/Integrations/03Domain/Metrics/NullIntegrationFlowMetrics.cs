using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Metrics;

/// <summary>
/// No-op metrics implementation used by default.
/// </summary>
public sealed class NullIntegrationFlowMetrics : IIntegrationFlowMetrics
{
    /// <summary>
    /// Shared no-op instance.
    /// </summary>
    public static NullIntegrationFlowMetrics Instance { get; } = new();

    /// <inheritdoc />
    public void RecordMessageProcessed(string profileName, TimeSpan duration, bool success)
    {
    }

    /// <inheritdoc />
    public void RecordOutboxRelayPublished(int count)
    {
    }

    /// <inheritdoc />
    public void RecordOutboxRelayFailed(int count)
    {
    }

    /// <inheritdoc />
    public void RecordOutboxRelayAbandoned(int count)
    {
    }

    /// <inheritdoc />
    public void RecordOutboxPending(int count)
    {
    }

    /// <inheritdoc />
    public void RecordRequestReply(string profileName, TimeSpan duration, bool success, bool timedOut = false)
    {
    }

    /// <inheritdoc />
    public void RecordRequestReplyRetryAfterTimeout(string profileName)
    {
    }

    /// <inheritdoc />
    public void RecordRpcPendingRelayPublished(int count)
    {
    }

    /// <inheritdoc />
    public void RecordRpcPendingRelayFailed(int count)
    {
    }

    /// <inheritdoc />
    public void RecordRpcPendingRelayAbandoned(int count)
    {
    }

    /// <inheritdoc />
    public void RecordRpcPendingAwaiting(int count)
    {
    }

    /// <inheritdoc />
    public void RecordRpcPendingCompleted(string profileName, TimeSpan duration, bool success, bool timedOut = false)
    {
    }
}
