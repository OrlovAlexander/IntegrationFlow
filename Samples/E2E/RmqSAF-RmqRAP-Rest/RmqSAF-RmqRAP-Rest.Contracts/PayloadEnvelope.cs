namespace RmqSAF_RmqRAP_Rest.Contracts;

public sealed record PayloadEnvelope(
    Guid Id,
    Guid CorrelationId,
    DateTimeOffset OccurredAt,
    string Type,
    object Data)
{
    public string MessageId => Id.ToString("N");

    public string CorrelationIdText => CorrelationId.ToString("N");
}

public static class IntegrationHeaderNames
{
    public const string CorrelationId = "X-Correlation-Id";

    public const string IdempotencyKey = "Idempotency-Key";

    public const string TraceParent = "traceparent";

    public const string TraceState = "tracestate";
}
