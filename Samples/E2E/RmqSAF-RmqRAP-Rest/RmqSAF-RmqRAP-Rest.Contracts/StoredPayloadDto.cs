namespace RmqSAF_RmqRAP_Rest.Contracts;

public sealed record StoredPayloadDto(
    string Id,
    string CorrelationId,
    DateTimeOffset ReceivedAt,
    string Body,
    IReadOnlyDictionary<string, string> SourceHeaders);
