namespace IntegrationFlow.Contexts.Integrations._01Infrastructure;

/// <summary>
/// Canonical structured log field names for IntegrationFlow transport events.
/// </summary>
public static class IntegrationStructuredLogFields
{
    public const string Profile = "integrationflow.profile";

    public const string MessageId = "integrationflow.message_id";

    public const string CorrelationId = "integrationflow.correlation_id";

    public const string DeliveryTag = "integrationflow.delivery_tag";

    public const string Kind = "integrationflow.kind";

    public const string Outcome = "integrationflow.outcome";
}
