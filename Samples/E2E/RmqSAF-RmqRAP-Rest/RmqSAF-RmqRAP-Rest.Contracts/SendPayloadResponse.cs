namespace RmqSAF_RmqRAP_Rest.Contracts;

public sealed record SendPayloadResponse(
    string MessageId,
    string CorrelationId,
    string Status,
    string TraceHint);
