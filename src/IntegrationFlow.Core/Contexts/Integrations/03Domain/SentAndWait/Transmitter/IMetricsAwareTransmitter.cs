using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter;

/// <summary>
/// Optional metrics hook for SentAndWait transmitters.
/// </summary>
internal interface IMetricsAwareTransmitter
{
    /// <summary>
    /// Metrics recorder for request-reply operations.
    /// </summary>
    IIntegrationFlowMetrics? Metrics { get; set; }
}
