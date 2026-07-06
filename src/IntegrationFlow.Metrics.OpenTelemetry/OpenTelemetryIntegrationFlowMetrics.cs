using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;

namespace IntegrationFlow.Metrics.OpenTelemetry;

/// <summary>
/// <see cref="IIntegrationFlowMetrics"/> implementation using <see cref="System.Diagnostics.Metrics.Meter"/>.
/// </summary>
public sealed class OpenTelemetryIntegrationFlowMetrics : IIntegrationFlowMetrics, IDisposable
{
    private readonly IntegrationFlowMeter meter;

    /// <summary>
    /// Creates metrics with default options.
    /// </summary>
    public OpenTelemetryIntegrationFlowMetrics()
        : this(new IntegrationFlowMetricsOptions())
    {
    }

    /// <summary>
    /// Creates metrics with the specified options.
    /// </summary>
    public OpenTelemetryIntegrationFlowMetrics(IntegrationFlowMetricsOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.MeterName))
        {
            throw new ArgumentException("Meter name is required.", nameof(options));
        }

        meter = new IntegrationFlowMeter(options.MeterName);
    }

    /// <inheritdoc />
    public void RecordMessageProcessed(string profileName, TimeSpan duration, bool success)
    {
        var profile = SanitizeProfile(profileName);
        var successTag = success ? "true" : "false";
        var tags = new[]
        {
            new KeyValuePair<string, object?>("profile", profile),
            new KeyValuePair<string, object?>("success", successTag),
        };

        meter.MessagesProcessed.Add(1, tags);
        meter.MessageProcessingDuration.Record(duration.TotalSeconds, tags);
    }

    /// <inheritdoc />
    public void RecordOutboxRelayPublished(int count)
    {
        if (count > 0)
        {
            meter.OutboxRelayPublished.Add(count);
        }
    }

    /// <inheritdoc />
    public void RecordOutboxRelayFailed(int count)
    {
        if (count > 0)
        {
            meter.OutboxRelayFailed.Add(count);
        }
    }

    /// <inheritdoc />
    public void RecordOutboxRelayAbandoned(int count)
    {
        if (count > 0)
        {
            meter.OutboxRelayAbandoned.Add(count);
        }
    }

    /// <inheritdoc />
    public void RecordOutboxPending(int count)
    {
        meter.SetPendingCount(count);
    }

    /// <inheritdoc />
    public void RecordRequestReply(string profileName, TimeSpan duration, bool success, bool timedOut = false)
    {
        var profile = SanitizeProfile(profileName);
        var tags = new[]
        {
            new KeyValuePair<string, object?>("profile", profile),
            new KeyValuePair<string, object?>("success", success ? "true" : "false"),
            new KeyValuePair<string, object?>("timeout", timedOut ? "true" : "false"),
        };

        meter.RequestReplyCompleted.Add(1, tags);
        meter.RequestReplyDuration.Record(duration.TotalSeconds, tags);
    }

    /// <inheritdoc />
    public void RecordRequestReplyRetryAfterTimeout(string profileName)
    {
        var profile = SanitizeProfile(profileName);
        meter.RequestReplyRetryAfterTimeout.Add(1, new KeyValuePair<string, object?>("profile", profile));
    }

    /// <inheritdoc />
    public void RecordRpcPendingRelayPublished(int count)
    {
        if (count > 0)
        {
            meter.RpcPendingRelayPublished.Add(count);
        }
    }

    /// <inheritdoc />
    public void RecordRpcPendingRelayFailed(int count)
    {
        if (count > 0)
        {
            meter.RpcPendingRelayFailed.Add(count);
        }
    }

    /// <inheritdoc />
    public void RecordRpcPendingRelayAbandoned(int count)
    {
        if (count > 0)
        {
            meter.RpcPendingRelayAbandoned.Add(count);
        }
    }

    /// <inheritdoc />
    public void RecordRpcPendingAwaiting(int count)
    {
        meter.SetRpcPendingAwaitingCount(count);
    }

    /// <inheritdoc />
    public void RecordRpcPendingCompleted(string profileName, TimeSpan duration, bool success, bool timedOut = false)
    {
        var profile = SanitizeProfile(profileName);
        var tags = new[]
        {
            new KeyValuePair<string, object?>("profile", profile),
            new KeyValuePair<string, object?>("success", success ? "true" : "false"),
            new KeyValuePair<string, object?>("timeout", timedOut ? "true" : "false"),
        };

        meter.RpcPendingCompleted.Add(1, tags);
        meter.RpcPendingDuration.Record(duration.TotalSeconds, tags);
    }

    /// <inheritdoc />
    public void RecordListenerReconnect(string profileName)
    {
        var profile = SanitizeProfile(profileName);
        meter.ListenerReconnect.Add(1, new KeyValuePair<string, object?>("profile", profile));
    }

    /// <inheritdoc />
    public void RecordListenerShutdownRequeue(string profileName)
    {
        var profile = SanitizeProfile(profileName);
        meter.ListenerShutdownRequeue.Add(1, new KeyValuePair<string, object?>("profile", profile));
    }

    /// <inheritdoc />
    public void RecordConnectionPoolSize(string kind, int size)
        => meter.SetConnectionPoolSize(kind, size);

    /// <inheritdoc />
    public void Dispose()
    {
        meter.Dispose();
    }

    internal static string SanitizeProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return "unknown";
        }

        return profileName.ToLowerInvariant().Replace('.', '_');
    }
}
