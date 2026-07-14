using RmqSAF_RmqRAP_Rest.Contracts;
using Sender.Api.Domain;

namespace Sender.Api.Application.SendPayload;

public sealed record SendPayloadRequest(string? Type, object? Data);

public sealed class SendPayloadCommandHandler
{
    private readonly IIntegrationPublisher publisher;
    private readonly ILogger<SendPayloadCommandHandler> logger;

    public SendPayloadCommandHandler(
        IIntegrationPublisher publisher,
        ILogger<SendPayloadCommandHandler> logger)
    {
        this.publisher = publisher;
        this.logger = logger;
    }

    public SendPayloadResponse Handle(SendPayloadRequest request, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var payload = new PayloadEnvelope(
            messageId,
            correlationId,
            DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(request.Type) ? "SampleEvent" : request.Type,
            request.Data ?? new { note = "empty payload" });

        var result = publisher.Publish(payload, cancellationToken);
        if (!result.Success)
        {
            logger.LogWarning("PayloadPublishFailed Reason={Reason}", result.FailureReason);
            throw new InvalidOperationException(result.FailureReason ?? "Publish failed.");
        }

        return new SendPayloadResponse(
            result.MessageId,
            result.CorrelationId,
            "accepted",
            "Open Aspire Dashboard → Traces → filter by correlationId");
    }
}
