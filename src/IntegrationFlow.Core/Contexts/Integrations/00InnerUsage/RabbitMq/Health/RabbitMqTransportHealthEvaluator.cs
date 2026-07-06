#if NET8_0_OR_GREATER
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;

internal static class RabbitMqTransportHealthEvaluator
{
    internal static HealthCheckResult EvaluateConnectionEndpoints(
        RabbitMqTransportKind kind,
        IReadOnlyList<RabbitMqTransportEndpointState> endpoints,
        RabbitMqHealthCheckOptions options)
    {
        if (endpoints.Count == 0)
        {
            return HealthCheckResult.Healthy($"No {kind} endpoints registered.");
        }

        var unhealthy = new List<string>();
        var degraded = new List<string>();

        foreach (var endpoint in endpoints)
        {
            switch (endpoint.Status)
            {
                case RabbitMqTransportConnectionStatus.Connected:
                    continue;

                case RabbitMqTransportConnectionStatus.Starting:
                    degraded.Add($"{endpoint.ProfileName}: starting");
                    continue;

                case RabbitMqTransportConnectionStatus.Reconnecting:
                    if (endpoint.ReconnectAttempts >= options.MaxReconnectAttemptsBeforeUnhealthy)
                    {
                        unhealthy.Add(
                            $"{endpoint.ProfileName}: reconnect attempts={endpoint.ReconnectAttempts}");
                    }
                    else
                    {
                        degraded.Add(
                            $"{endpoint.ProfileName}: reconnecting (attempt {endpoint.ReconnectAttempts})");
                    }

                    continue;

                default:
                    unhealthy.Add($"{endpoint.ProfileName}: status={endpoint.Status}");
                    break;
            }
        }

        var data = BuildData(endpoints);

        if (unhealthy.Count > 0)
        {
            return HealthCheckResult.Unhealthy(
                $"{kind} unhealthy: {string.Join("; ", unhealthy)}",
                data: data);
        }

        if (degraded.Count > 0)
        {
            return HealthCheckResult.Degraded(
                $"{kind} degraded: {string.Join("; ", degraded)}",
                data: data);
        }

        return HealthCheckResult.Healthy($"{kind} connected.", data: data);
    }

    internal static HealthCheckResult EvaluateOutboxRelay(
        IReadOnlyList<RabbitMqTransportEndpointState> endpoints,
        RabbitMqHealthCheckOptions options)
    {
        if (endpoints.Count == 0)
        {
            return HealthCheckResult.Healthy("Outbox relay not registered.");
        }

        var endpoint = endpoints[0];
        var data = BuildData(endpoints);

        if (endpoint.ConsecutiveFailures >= options.OutboxRelayMaxConsecutiveFailures)
        {
            return HealthCheckResult.Unhealthy(
                $"Outbox relay unhealthy: consecutiveFailures={endpoint.ConsecutiveFailures}",
                data: data);
        }

        if (endpoint.Status == RabbitMqTransportConnectionStatus.Starting)
        {
            return HealthCheckResult.Degraded("Outbox relay starting.", data: data);
        }

        if (endpoint.ConsecutiveFailures > 0)
        {
            return HealthCheckResult.Degraded(
                $"Outbox relay degraded: consecutiveFailures={endpoint.ConsecutiveFailures}",
                data: data);
        }

        return HealthCheckResult.Healthy("Outbox relay healthy.", data: data);
    }

    private static IReadOnlyDictionary<string, object> BuildData(IReadOnlyList<RabbitMqTransportEndpointState> endpoints)
        => endpoints.ToDictionary(
            endpoint => endpoint.ProfileName,
            endpoint => (object)new
            {
                endpoint.Status,
                endpoint.ReconnectAttempts,
                endpoint.ConsecutiveFailures,
                endpoint.LastConnectedAtUtc,
                endpoint.LastSuccessfulOperationAtUtc,
                endpoint.LastError
            });
}
#endif
