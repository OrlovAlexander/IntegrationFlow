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
}
