namespace IntegrationFlow.Contexts.Integrations._03Domain.Metrics;

/// <summary>
/// Canonical <c>reason</c> tag values for <see cref="IIntegrationFlowMetrics.RecordConsumerOutcome"/>.
/// </summary>
public static class ConsumerOutcomeReason
{
    public const string Nack = "nack";

    public const string Requeue = "requeue";

    public const string DedupSkip = "dedup_skip";

    public const string InProgressRequeue = "in_progress_requeue";
}
