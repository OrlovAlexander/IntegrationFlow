using System.Net;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess;

/// <summary>
/// Outcome of inbound webhook processing mapped to HTTP response semantics.
/// </summary>
public enum RestWebhookProcessResult
{
    /// <summary>
    /// Handler completed successfully — respond with 200.
    /// </summary>
    Success,

    /// <summary>
    /// Duplicate delivery skipped by dedup store — respond with 200 (idempotent).
    /// </summary>
    DuplicateSkipped,

    /// <summary>
    /// Same message is being processed concurrently — respond with 503 (partner retry).
    /// </summary>
    InProgress,

    /// <summary>
    /// Business handler failed — respond with 500 (partner retry).
    /// </summary>
    HandlerFailed,

    /// <summary>
    /// Authentication hook rejected the request — respond with 401.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// Request body exceeds configured limit — respond with 413.
    /// </summary>
    PayloadTooLarge,

    /// <summary>
    /// Required message id header is missing — respond with 400.
    /// </summary>
    MissingMessageId,

    /// <summary>
    /// HTTP method is not allowed for the profile — respond with 405.
    /// </summary>
    MethodNotAllowed,
}

/// <summary>
/// Maps <see cref="RestWebhookProcessResult"/> to HTTP status codes.
/// </summary>
public static class RestWebhookProcessResultMapper
{
    public static int ToStatusCode(RestWebhookProcessResult result)
        => result switch
        {
            RestWebhookProcessResult.Success => (int)HttpStatusCode.OK,
            RestWebhookProcessResult.DuplicateSkipped => (int)HttpStatusCode.OK,
            RestWebhookProcessResult.InProgress => (int)HttpStatusCode.ServiceUnavailable,
            RestWebhookProcessResult.HandlerFailed => (int)HttpStatusCode.InternalServerError,
            RestWebhookProcessResult.Unauthorized => (int)HttpStatusCode.Unauthorized,
            RestWebhookProcessResult.PayloadTooLarge => (int)HttpStatusCode.RequestEntityTooLarge,
            RestWebhookProcessResult.MissingMessageId => (int)HttpStatusCode.BadRequest,
            RestWebhookProcessResult.MethodNotAllowed => (int)HttpStatusCode.MethodNotAllowed,
            _ => (int)HttpStatusCode.InternalServerError,
        };
}
