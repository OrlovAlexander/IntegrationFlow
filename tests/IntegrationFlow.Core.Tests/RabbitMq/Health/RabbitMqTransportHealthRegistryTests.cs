using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using Xunit;

namespace IntegrationFlow.Core.Tests.RabbitMq.Health;

public sealed class RabbitMqTransportHealthRegistryTests
{
    [Fact]
    public void Register_InitializesStartingState()
    {
        var registry = new RabbitMqTransportHealthRegistry();
        registry.Register(RabbitMqTransportKind.Listener, "Inbox");

        var endpoints = registry.GetRegisteredEndpoints(RabbitMqTransportKind.Listener);

        Assert.Single(endpoints);
        Assert.Equal(RabbitMqTransportConnectionStatus.Starting, endpoints[0].Status);
    }

    [Fact]
    public void ReportConnected_ResetsReconnectAttempts()
    {
        var registry = new RabbitMqTransportHealthRegistry();
        registry.Register(RabbitMqTransportKind.Listener, "Inbox");
        registry.ReportReconnecting(RabbitMqTransportKind.Listener, "Inbox", attempt: 3);

        registry.ReportConnected(RabbitMqTransportKind.Listener, "Inbox");

        var endpoint = registry.GetRegisteredEndpoints(RabbitMqTransportKind.Listener)[0];
        Assert.Equal(RabbitMqTransportConnectionStatus.Connected, endpoint.Status);
        Assert.Equal(0, endpoint.ReconnectAttempts);
    }

    [Fact]
    public void ReportConnected_PublishesBrokerConnectedMetric()
    {
        var metrics = new TestBrokerMetrics();
        var registry = new RabbitMqTransportHealthRegistry();
        registry.SetMetrics(metrics);
        registry.Register(RabbitMqTransportKind.Listener, "Inbox");

        registry.ReportConnected(RabbitMqTransportKind.Listener, "Inbox");

        Assert.Equal(1, metrics.LastConnectedValue);
        Assert.Equal("Inbox", metrics.LastProfileName);
        Assert.Equal("listener", metrics.LastKind);
    }

    [Fact]
    public void ReportReconnecting_PublishesDisconnectedMetric()
    {
        var metrics = new TestBrokerMetrics();
        var registry = new RabbitMqTransportHealthRegistry();
        registry.SetMetrics(metrics);
        registry.Register(RabbitMqTransportKind.Listener, "Inbox");
        registry.ReportConnected(RabbitMqTransportKind.Listener, "Inbox");

        registry.ReportReconnecting(RabbitMqTransportKind.Listener, "Inbox", attempt: 1);

        Assert.Equal(0, metrics.LastConnectedValue);
    }

    [Fact]
    public void ReportOutboxRelayBatchFailure_IncrementsConsecutiveFailures()
    {
        var registry = new RabbitMqTransportHealthRegistry();
        registry.Register(RabbitMqTransportKind.OutboxRelay, RabbitMqTransportHealthRegistry.OutboxRelayProfileName);

        registry.ReportOutboxRelayBatchFailure("publish failed");
        registry.ReportOutboxRelayBatchFailure("publish failed");

        var endpoint = registry.GetRegisteredEndpoints(RabbitMqTransportKind.OutboxRelay)[0];
        Assert.Equal(2, endpoint.ConsecutiveFailures);
        Assert.Equal(RabbitMqTransportConnectionStatus.Disconnected, endpoint.Status);
    }

    private sealed class TestBrokerMetrics : IIntegrationFlowMetrics
    {
        public int? LastConnectedValue { get; private set; }

        public string? LastProfileName { get; private set; }

        public string? LastKind { get; private set; }

        public void RecordMessageProcessed(string profileName, TimeSpan duration, bool success)
        {
        }

        public void RecordOutboxRelayPublished(int count)
        {
        }

        public void RecordOutboxRelayFailed(int count)
        {
        }

        public void RecordOutboxRelayAbandoned(int count)
        {
        }

        public void RecordOutboxPending(int count)
        {
        }

        public void RecordRequestReply(string profileName, TimeSpan duration, bool success, bool timedOut = false, string? transport = null)
        {
        }

        public void RecordRequestReplyRetryAfterTimeout(string profileName)
        {
        }

        public void RecordRpcPendingRelayPublished(int count)
        {
        }

        public void RecordRpcPendingRelayFailed(int count)
        {
        }

        public void RecordRpcPendingRelayAbandoned(int count)
        {
        }

        public void RecordRpcPendingAwaiting(int count)
        {
        }

        public void RecordRpcPendingCompleted(string profileName, TimeSpan duration, bool success, bool timedOut = false)
        {
        }

        public void RecordListenerReconnect(string profileName)
        {
        }

        public void RecordListenerShutdownRequeue(string profileName)
        {
        }

        public void RecordConsumerOutcome(string profileName, string reason)
        {
        }

        public void RecordConnectionPoolSize(string kind, int size)
        {
        }

        public void RecordBrokerConnected(string profileName, string kind, bool connected)
        {
            LastProfileName = profileName;
            LastKind = kind;
            LastConnectedValue = connected ? 1 : 0;
        }
    }
}
