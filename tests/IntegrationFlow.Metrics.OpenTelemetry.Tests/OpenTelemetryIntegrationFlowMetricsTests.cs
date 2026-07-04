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
