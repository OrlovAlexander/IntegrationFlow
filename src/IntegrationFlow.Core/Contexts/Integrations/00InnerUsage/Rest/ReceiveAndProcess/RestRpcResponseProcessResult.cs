using System.Net;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess;

/// <summary>
/// Outcome of REST async RPC response correlation.
/// </summary>
public enum RestRpcResponseProcessResult
{
    Completed,
    DuplicateSkipped,
    InvalidCorrelationId,
    PendingNotFound,
    InvalidPendingState,
    HandlerFailed,
    Unauthorized,
    PayloadTooLarge,
    MissingMessageId,
    MethodNotAllowed,
}

/// <summary>
/// Maps <see cref="RestRpcResponseProcessResult"/> to HTTP status codes.
/// </summary>
public static class RestRpcResponseProcessResultMapper
{
    public static int ToStatusCode(RestRpcResponseProcessResult result)
        => result switch
        {
            RestRpcResponseProcessResult.Completed => (int)HttpStatusCode.OK,
            RestRpcResponseProcessResult.DuplicateSkipped => (int)HttpStatusCode.OK,
            RestRpcResponseProcessResult.InvalidCorrelationId => (int)HttpStatusCode.BadRequest,
            RestRpcResponseProcessResult.PendingNotFound => (int)HttpStatusCode.NotFound,
            RestRpcResponseProcessResult.InvalidPendingState => (int)HttpStatusCode.Conflict,
            RestRpcResponseProcessResult.HandlerFailed => (int)HttpStatusCode.InternalServerError,
            RestRpcResponseProcessResult.Unauthorized => (int)HttpStatusCode.Unauthorized,
            RestRpcResponseProcessResult.PayloadTooLarge => (int)HttpStatusCode.RequestEntityTooLarge,
            RestRpcResponseProcessResult.MissingMessageId => (int)HttpStatusCode.BadRequest,
            RestRpcResponseProcessResult.MethodNotAllowed => (int)HttpStatusCode.MethodNotAllowed,
            _ => (int)HttpStatusCode.InternalServerError,
        };
}
