using System.Diagnostics.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Metrics.OpenTelemetry.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationFlow.Metrics.OpenTelemetry.Tests;

public sealed class OpenTelemetryIntegrationFlowMetricsTests
{
    [Fact]
    public void RecordMessageProcessed_IncrementsCounterAndHistogram()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordMessageProcessed("Inbox", TimeSpan.FromMilliseconds(250), success: true);
        collector.Collect();

        Assert.Equal(1, collector.GetCounterSum("integrationflow.message.processed"));
        Assert.True(collector.GetHistogramCount("integrationflow.message.processing.duration") >= 1);
        Assert.Contains(
            collector.GetCounterTags("integrationflow.message.processed"),
            tags => tags.TryGetValue("profile", out var profile) && profile?.ToString() == "inbox");
    }

    [Fact]
    public void RecordOutboxRelay_IncrementsBatchCounters()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordOutboxRelayPublished(3);
        metrics.RecordOutboxRelayFailed(2);
        metrics.RecordOutboxRelayAbandoned(1);
        collector.Collect();

        Assert.Equal(3, collector.GetCounterSum("integrationflow.outbox.relay.published"));
        Assert.Equal(2, collector.GetCounterSum("integrationflow.outbox.relay.failed"));
        Assert.Equal(1, collector.GetCounterSum("integrationflow.outbox.relay.abandoned"));
    }

    [Fact]
    public void RecordOutboxPending_UpdatesGauge()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordOutboxPending(42);
        collector.Collect();

        Assert.Equal(42, collector.GetGaugeValue("integrationflow.outbox.pending"));
    }

    [Fact]
    public void RecordOutboxRelay_IgnoresZeroOrNegativeCounts()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordOutboxRelayPublished(0);
        metrics.RecordOutboxRelayFailed(-1);
        collector.Collect();

        Assert.Equal(0, collector.GetCounterSum("integrationflow.outbox.relay.published"));
        Assert.Equal(0, collector.GetCounterSum("integrationflow.outbox.relay.failed"));
    }

    [Fact]
    public void RecordRequestReply_IncrementsCounterAndHistogram()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordRequestReply("OrdersRpc", TimeSpan.FromMilliseconds(120), success: true);
        collector.Collect();

        Assert.Equal(1, collector.GetCounterSum("integrationflow.requestreply.completed"));
        Assert.True(collector.GetHistogramCount("integrationflow.requestreply.duration") >= 1);
        Assert.Contains(
            collector.GetCounterTags("integrationflow.requestreply.completed"),
            tags => tags.TryGetValue("profile", out var profile) && profile?.ToString() == "ordersrpc");
    }

    [Fact]
    public void RecordRequestReply_RecordsTimeoutTag()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordRequestReply("OrdersRpc", TimeSpan.FromSeconds(30), success: false, timedOut: true);
        collector.Collect();

        Assert.Contains(
            collector.GetCounterTags("integrationflow.requestreply.completed"),
            tags => tags.TryGetValue("timeout", out var timeout) && timeout?.ToString() == "true");
    }

    [Fact]
    public void RecordRequestReplyRetryAfterTimeout_IncrementsCounter()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordRequestReplyRetryAfterTimeout("OrdersRpc");
        collector.Collect();

        Assert.Equal(1, collector.GetCounterSum("integrationflow.requestreply.retry_after_timeout"));
    }

    [Fact]
    public void RecordRpcPendingRelay_IncrementsBatchCounters()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordRpcPendingRelayPublished(2);
        metrics.RecordRpcPendingRelayFailed(1);
        metrics.RecordRpcPendingRelayAbandoned(1);
        collector.Collect();

        Assert.Equal(2, collector.GetCounterSum("integrationflow.rpc.pending.relay.published"));
        Assert.Equal(1, collector.GetCounterSum("integrationflow.rpc.pending.relay.failed"));
        Assert.Equal(1, collector.GetCounterSum("integrationflow.rpc.pending.relay.abandoned"));
    }

    [Fact]
    public void RecordRpcPendingAwaiting_UpdatesGauge()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordRpcPendingAwaiting(7);
        collector.Collect();

        Assert.Equal(7, collector.GetGaugeValue("integrationflow.rpc.pending.awaiting"));
    }

    [Fact]
    public void RecordRpcPendingCompleted_IncrementsCounterAndHistogram()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordRpcPendingCompleted("OrdersRpcAsync", TimeSpan.FromMilliseconds(350), success: true);
        collector.Collect();

        Assert.Equal(1, collector.GetCounterSum("integrationflow.rpc.pending.completed"));
        Assert.True(collector.GetHistogramCount("integrationflow.rpc.pending.duration") >= 1);
    }

    [Fact]
    public void RecordListenerTransportMetrics_IncrementsCounters()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordListenerReconnect("Inbox");
        metrics.RecordListenerShutdownRequeue("Inbox");
        collector.Collect();

        Assert.Equal(1, collector.GetCounterSum("integrationflow.listener.reconnect"));
        Assert.Equal(1, collector.GetCounterSum("integrationflow.message.shutdown_requeue"));
    }

    [Fact]
    public void RecordConnectionPoolSize_UpdatesGauge()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordConnectionPoolSize("rpc", 2);
        metrics.RecordConnectionPoolSize("publish", 3);
        collector.Collect();

        var measurements = collector.GetGaugeMeasurements("integrationflow.connection.pool.size");
        Assert.Contains(measurements, item => item.Value == 2 && item.Tags["kind"]?.ToString() == "rpc");
        Assert.Contains(measurements, item => item.Value == 3 && item.Tags["kind"]?.ToString() == "publish");
    }

    [Fact]
    public void RecordBrokerConnected_UpdatesGauge()
    {
        using var collector = new MetricCollector("IntegrationFlow");
        using var metrics = new OpenTelemetryIntegrationFlowMetrics();

        metrics.RecordBrokerConnected("Inbox", "listener", connected: true);
        metrics.RecordBrokerConnected("Inbox", "listener", connected: false);
        collector.Collect();

        var measurements = collector.GetGaugeMeasurements("integrationflow.broker.connected");
        Assert.Contains(
            measurements,
            item => item.Value == 0
                && item.Tags["profile"]?.ToString() == "inbox"
                && item.Tags["kind"]?.ToString() == "listener");
    }

    [Theory]
    [InlineData("Orders.Inbox", "orders_inbox")]
    [InlineData("", "unknown")]
    [InlineData("   ", "unknown")]
    public void SanitizeProfile_NormalizesValue(string input, string expected)
    {
        Assert.Equal(expected, OpenTelemetryIntegrationFlowMetrics.SanitizeProfile(input));
    }

    [Fact]
    public void AddIntegrationFlowOpenTelemetryMetrics_ReplacesNullImplementation()
    {
        var services = new ServiceCollection();
        services.AddIntegrationFlowOpenTelemetryMetrics(options => options.MeterName = "IntegrationFlow.Test");

        var metrics = services.BuildServiceProvider().GetRequiredService<IIntegrationFlowMetrics>();

        Assert.IsType<OpenTelemetryIntegrationFlowMetrics>(metrics);
    }

    private sealed class MetricCollector : IDisposable
    {
        private readonly MeterListener listener;
        private readonly Dictionary<string, long> counterSums = new();
        private readonly Dictionary<string, int> histogramCounts = new();
        private readonly Dictionary<string, long> gaugeValues = new();
        private readonly List<IReadOnlyDictionary<string, object?>> counterTagSets = new();
        private readonly List<(string InstrumentName, long Value, IReadOnlyDictionary<string, object?> Tags)> gaugeTagMeasurements = new();

        public MetricCollector(string meterName)
        {
            listener = new MeterListener
            {
                InstrumentPublished = (instrument, meterListener) =>
                {
                    if (instrument.Meter.Name == meterName)
                    {
                        meterListener.EnableMeasurementEvents(instrument);
                    }
                },
            };

            listener.SetMeasurementEventCallback<long>(RecordLongMeasurement);
            listener.SetMeasurementEventCallback<double>(RecordDoubleMeasurement);
            listener.Start();
        }

        public void Collect()
        {
            listener.RecordObservableInstruments();
        }

        public long GetCounterSum(string instrumentName)
            => counterSums.TryGetValue(instrumentName, out var value) ? value : 0;

        public int GetHistogramCount(string instrumentName)
            => histogramCounts.TryGetValue(instrumentName, out var value) ? value : 0;

        public long GetGaugeValue(string instrumentName)
            => gaugeValues.TryGetValue(instrumentName, out var value) ? value : 0;

        public IReadOnlyList<(long Value, IReadOnlyDictionary<string, object?> Tags)> GetGaugeMeasurements(string instrumentName)
            => gaugeTagMeasurements
                .Where(item => item.InstrumentName == instrumentName)
                .Select(item => (item.Value, item.Tags))
                .ToList();

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> GetCounterTags(string instrumentName)
            => counterTagSets;

        public void Dispose()
        {
            listener.Dispose();
        }

        private void RecordLongMeasurement(
            Instrument instrument,
            long measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
        {
            if (instrument is Counter<long>)
            {
                counterSums[instrument.Name] = GetCounterSum(instrument.Name) + measurement;
                counterTagSets.Add(ToDictionary(tags));
            }
            else if (instrument.Name.Contains("pending", StringComparison.Ordinal))
            {
                gaugeValues[instrument.Name] = measurement;
            }
            else
            {
                gaugeTagMeasurements.Add((instrument.Name, measurement, ToDictionary(tags)));
            }
        }

        private void RecordDoubleMeasurement(
            Instrument instrument,
            double measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
        {
            if (instrument is Histogram<double>)
            {
                histogramCounts[instrument.Name] = GetHistogramCount(instrument.Name) + 1;
            }
        }

        private static Dictionary<string, object?> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var result = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                result[tag.Key] = tag.Value;
            }

            return result;
        }
    }
}
