using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;

/// <summary>
/// Thread-safe registry of RabbitMQ transport connection health.
/// </summary>
public sealed class RabbitMqTransportHealthRegistry
{
    internal const string OutboxRelayProfileName = "outbox_relay";

    private readonly ConcurrentDictionary<string, RabbitMqTransportEndpointState> endpoints = new(StringComparer.OrdinalIgnoreCase);
    private IIntegrationFlowMetrics? metrics;

    public void SetMetrics(IIntegrationFlowMetrics? integrationFlowMetrics)
        => metrics = integrationFlowMetrics;

    public void Register(RabbitMqTransportKind kind, string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name is required.", nameof(profileName));
        }

        var key = BuildKey(kind, profileName);
        endpoints.AddOrUpdate(
            key,
            _ => new RabbitMqTransportEndpointState
            {
                Kind = kind,
                ProfileName = profileName,
                Status = RabbitMqTransportConnectionStatus.Starting,
                IsRegistered = true
            },
            (_, existing) =>
            {
                existing.IsRegistered = true;
                if (existing.Status == RabbitMqTransportConnectionStatus.Unknown)
                {
                    existing.Status = RabbitMqTransportConnectionStatus.Starting;
                }

                return existing;
            });

        PublishBrokerConnectedMetric(kind, profileName, RabbitMqTransportConnectionStatus.Starting);
    }

    public void ReportConnected(RabbitMqTransportKind kind, string profileName)
    {
        var state = GetOrCreate(kind, profileName);
        state.Status = RabbitMqTransportConnectionStatus.Connected;
        state.ReconnectAttempts = 0;
        state.ConsecutiveFailures = 0;
        state.LastConnectedAtUtc = DateTimeOffset.UtcNow;
        state.LastSuccessfulOperationAtUtc = DateTimeOffset.UtcNow;
        state.LastError = null;
        PublishBrokerConnectedMetric(kind, profileName, state.Status);
    }

    public void ReportReconnecting(RabbitMqTransportKind kind, string profileName, int attempt, string? error = null)
    {
        var state = GetOrCreate(kind, profileName);
        state.Status = RabbitMqTransportConnectionStatus.Reconnecting;
        state.ReconnectAttempts = attempt;
        state.LastError = error;
        PublishBrokerConnectedMetric(kind, profileName, state.Status);
    }

    public void ReportDisconnected(RabbitMqTransportKind kind, string profileName, string? error = null)
    {
        var state = GetOrCreate(kind, profileName);
        state.Status = RabbitMqTransportConnectionStatus.Disconnected;
        state.LastError = error;
        PublishBrokerConnectedMetric(kind, profileName, state.Status);
    }

    public void ReportStopped(RabbitMqTransportKind kind, string profileName)
    {
        var state = GetOrCreate(kind, profileName);
        state.Status = RabbitMqTransportConnectionStatus.Stopped;
        PublishBrokerConnectedMetric(kind, profileName, state.Status);
    }

    public void ReportOutboxRelayBatchSuccess()
    {
        var state = GetOrCreate(RabbitMqTransportKind.OutboxRelay, OutboxRelayProfileName);
        state.Status = RabbitMqTransportConnectionStatus.Connected;
        state.ConsecutiveFailures = 0;
        state.LastSuccessfulOperationAtUtc = DateTimeOffset.UtcNow;
        state.LastError = null;
        PublishBrokerConnectedMetric(RabbitMqTransportKind.OutboxRelay, OutboxRelayProfileName, state.Status);
    }

    public void ReportOutboxRelayBatchFailure(string? error)
    {
        var state = GetOrCreate(RabbitMqTransportKind.OutboxRelay, OutboxRelayProfileName);
        state.ConsecutiveFailures++;
        state.LastError = error;
        state.Status = RabbitMqTransportConnectionStatus.Disconnected;
        PublishBrokerConnectedMetric(RabbitMqTransportKind.OutboxRelay, OutboxRelayProfileName, state.Status);
    }

    public IReadOnlyList<RabbitMqTransportEndpointState> GetRegisteredEndpoints(RabbitMqTransportKind kind)
        => endpoints.Values
            .Where(endpoint => endpoint.Kind == kind && endpoint.IsRegistered)
            .OrderBy(endpoint => endpoint.ProfileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static string BuildKey(RabbitMqTransportKind kind, string profileName)
        => $"{kind}:{profileName}";

    internal static bool IsBrokerConnected(RabbitMqTransportConnectionStatus status)
        => status == RabbitMqTransportConnectionStatus.Connected;

    private RabbitMqTransportEndpointState GetOrCreate(RabbitMqTransportKind kind, string profileName)
    {
        var key = BuildKey(kind, profileName);
        return endpoints.GetOrAdd(
            key,
            _ => new RabbitMqTransportEndpointState
            {
                Kind = kind,
                ProfileName = profileName,
                Status = RabbitMqTransportConnectionStatus.Unknown
            });
    }

    private void PublishBrokerConnectedMetric(
        RabbitMqTransportKind kind,
        string profileName,
        RabbitMqTransportConnectionStatus status)
        => metrics?.RecordBrokerConnected(
            profileName,
            kind.ToMetricKind(),
            IsBrokerConnected(status));
}
