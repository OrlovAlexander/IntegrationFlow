using System;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;

/// <summary>
/// Snapshot of health state for a RabbitMQ transport endpoint.
/// </summary>
public sealed class RabbitMqTransportEndpointState
{
    public RabbitMqTransportKind Kind { get; set; }

    public string ProfileName { get; set; } = string.Empty;

    public RabbitMqTransportConnectionStatus Status { get; set; } = RabbitMqTransportConnectionStatus.Unknown;

    public int ReconnectAttempts { get; set; }

    public int ConsecutiveFailures { get; set; }

    public DateTimeOffset? LastConnectedAtUtc { get; set; }

    public DateTimeOffset? LastSuccessfulOperationAtUtc { get; set; }

    public string? LastError { get; set; }

    public bool IsRegistered { get; set; }
}
