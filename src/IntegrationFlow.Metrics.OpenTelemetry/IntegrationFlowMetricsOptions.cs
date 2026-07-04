namespace IntegrationFlow.Metrics.OpenTelemetry;

/// <summary>
/// Options for IntegrationFlow OpenTelemetry metrics.
/// </summary>
public sealed class IntegrationFlowMetricsOptions
{
    /// <summary>
    /// Meter name registered with <see cref="System.Diagnostics.Metrics.Meter"/>.
    /// </summary>
    public string MeterName { get; set; } = "IntegrationFlow";
}
