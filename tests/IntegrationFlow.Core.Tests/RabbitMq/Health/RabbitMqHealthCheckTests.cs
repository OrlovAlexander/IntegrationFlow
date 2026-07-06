using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntegrationFlow.Core.Tests.RabbitMq.Health;

public sealed class RabbitMqHealthCheckTests
{
    [Fact]
    public async Task ListenerHealthCheck_ReturnsHealthyWhenConnected()
    {
        var registry = new RabbitMqTransportHealthRegistry();
        registry.Register(RabbitMqTransportKind.Listener, "Inbox");
        registry.ReportConnected(RabbitMqTransportKind.Listener, "Inbox");

        var check = new RabbitMqListenerHealthCheck(
            registry,
            Options.Create(new RabbitMqHealthCheckOptions()));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ListenerHealthCheck_ReturnsUnhealthyWhenReconnectAttemptsExceeded()
    {
        var registry = new RabbitMqTransportHealthRegistry();
        registry.Register(RabbitMqTransportKind.Listener, "Inbox");
        registry.ReportReconnecting(RabbitMqTransportKind.Listener, "Inbox", attempt: 5);

        var check = new RabbitMqListenerHealthCheck(
            registry,
            Options.Create(new RabbitMqHealthCheckOptions { MaxReconnectAttemptsBeforeUnhealthy = 5 }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task ListenerHealthCheck_ReturnsDegradedWhenReconnectingBelowThreshold()
    {
        var registry = new RabbitMqTransportHealthRegistry();
        registry.Register(RabbitMqTransportKind.Listener, "Inbox");
        registry.ReportReconnecting(RabbitMqTransportKind.Listener, "Inbox", attempt: 2);

        var check = new RabbitMqListenerHealthCheck(
            registry,
            Options.Create(new RabbitMqHealthCheckOptions { MaxReconnectAttemptsBeforeUnhealthy = 5 }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task OutboxRelayHealthCheck_ReturnsUnhealthyWhenConsecutiveFailuresExceeded()
    {
        var registry = new RabbitMqTransportHealthRegistry();
        registry.Register(RabbitMqTransportKind.OutboxRelay, RabbitMqTransportHealthRegistry.OutboxRelayProfileName);

        for (var i = 0; i < 5; i++)
        {
            registry.ReportOutboxRelayBatchFailure("failed");
        }

        var check = new RabbitMqOutboxRelayHealthCheck(
            registry,
            Options.Create(new RabbitMqHealthCheckOptions { OutboxRelayMaxConsecutiveFailures = 5 }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task RpcCorrelationHealthCheck_ReturnsHealthyWhenNotRegistered()
    {
        var registry = new RabbitMqTransportHealthRegistry();
        var check = new RabbitMqRpcCorrelationHealthCheck(
            registry,
            Options.Create(new RabbitMqHealthCheckOptions()));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void EvaluateConnectionEndpoints_IncludesEndpointData()
    {
        var endpoints = new List<RabbitMqTransportEndpointState>
        {
            new()
            {
                ProfileName = "Inbox",
                Status = RabbitMqTransportConnectionStatus.Connected,
                IsRegistered = true
            }
        };

        var result = RabbitMqTransportHealthEvaluator.EvaluateConnectionEndpoints(
            RabbitMqTransportKind.Listener,
            endpoints,
            new RabbitMqHealthCheckOptions());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(result.Data.ContainsKey("Inbox"));
    }
}
