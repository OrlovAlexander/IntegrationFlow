using System;
using System.Linq;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// REST request-reply configuration for SentAndWait.
/// </summary>
public sealed class RestRequestReplyConfiguration : IConfiguration, IRestConnectionConfiguration
{
    /// <summary>
    /// Profile name in rest.json.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Base URL (e.g. https://api.partner.com/).
    /// </summary>
    public string BaseAddress { get; set; } = string.Empty;

    /// <summary>
    /// Request path (e.g. /v1/orders/lookup).
    /// </summary>
    public string RequestPath { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method.
    /// </summary>
    public string Method { get; set; } = "POST";

    /// <summary>
    /// Request Content-Type header.
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Accept header.
    /// </summary>
    public string Accept { get; set; } = "application/json";

    /// <summary>
    /// Response timeout in seconds.
    /// </summary>
    public int ResponseTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Default HTTP timeout in seconds (from shared connection profile).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Max concurrent in-flight requests (0 = unlimited).
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 1;

    /// <summary>
    /// Header name for idempotency key from <see cref="TransmitData.MessageId"/>.
    /// </summary>
    public string IdempotencyHeaderName { get; set; } = "Idempotency-Key";

    /// <summary>
    /// Bearer token for Authorization header.
    /// </summary>
    public string BearerToken { get; set; } = string.Empty;

    public string BasicAuthUser { get; set; } = string.Empty;

    public string BasicAuthPassword { get; set; } = string.Empty;

    public string ApiKeyHeaderName { get; set; } = string.Empty;

    public string ApiKeyHeaderValue { get; set; } = string.Empty;

    public string ClientCertificatePath { get; set; } = string.Empty;

    public string ClientCertificatePassword { get; set; } = string.Empty;

    public string TlsServerName { get; set; } = string.Empty;

    public string HealthCheckPath { get; set; } = string.Empty;

    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Request-reply mode: sync HTTP or async outbox with callback webhook.
    /// </summary>
    public RestRequestReplyRequestMode RequestMode { get; set; } = RestRequestReplyRequestMode.Sync;

    /// <summary>
    /// REST webhook profile name for async RPC responses (<see cref="RestWebhookConfiguration"/>).
    /// Required when <see cref="RequestMode"/> is AsyncOutbox.
    /// </summary>
    public string ResponseWebhookProfileName { get; set; } = string.Empty;

    /// <summary>
    /// Public base URL of this application used to build callback URL for partner systems.
    /// Required when <see cref="RequestMode"/> is AsyncOutbox.
    /// </summary>
    public string ResponseCallbackBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Header name for callback URL sent to partner on async outbox relay.
    /// </summary>
    public string CallbackUrlHeaderName { get; set; } = "X-Callback-Url";

    /// <summary>
    /// Header name for correlation id (pending request id) sent on async outbox relay.
    /// </summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-Id";

    /// <summary>
    /// HTTP status codes treated as request accepted for async processing during relay.
    /// </summary>
    public int[] AcceptedStatusCodes { get; set; } = { 200, 202, 204 };

    /// <summary>
    /// SLA for awaiting async response before timeout.
    /// </summary>
    public int PendingTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Retry HTTP 5xx, 429 and connection errors.
    /// </summary>
    public bool RetryOnTransientErrors { get; set; } = true;

    /// <summary>
    /// Extra attempts after the first try for transient HTTP/connection errors.
    /// </summary>
    public int MaxTransientRetries { get; set; } = 1;

    /// <summary>
    /// Validates configuration.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseAddress))
        {
            throw new InvalidOperationException("BaseAddress is required for REST request-reply.");
        }

        if (string.IsNullOrWhiteSpace(RequestPath))
        {
            throw new InvalidOperationException("RequestPath is required for REST request-reply.");
        }

        if (string.IsNullOrWhiteSpace(Method))
        {
            throw new InvalidOperationException("Method is required for REST request-reply.");
        }

        if (ResponseTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("ResponseTimeoutSeconds must be greater than 0.");
        }

        if (MaxTransientRetries < 0)
        {
            throw new InvalidOperationException("MaxTransientRetries must be greater than or equal to 0.");
        }

        if (RequestMode == RestRequestReplyRequestMode.AsyncOutbox)
        {
            if (string.IsNullOrWhiteSpace(ResponseWebhookProfileName))
            {
                throw new InvalidOperationException(
                    "ResponseWebhookProfileName is required for RequestMode=AsyncOutbox.");
            }

            if (string.IsNullOrWhiteSpace(ResponseCallbackBaseUrl))
            {
                throw new InvalidOperationException(
                    "ResponseCallbackBaseUrl is required for RequestMode=AsyncOutbox.");
            }

            if (PendingTimeoutSeconds <= 0)
            {
                throw new InvalidOperationException("PendingTimeoutSeconds must be greater than 0.");
            }

            if (AcceptedStatusCodes == null || AcceptedStatusCodes.Length == 0)
            {
                throw new InvalidOperationException(
                    "AcceptedStatusCodes must contain at least one status code for AsyncOutbox mode.");
            }
        }
    }

    internal bool IsAcceptedStatusCode(int statusCode)
        => AcceptedStatusCodes != null &&
           AcceptedStatusCodes.Any(code => code == statusCode);

    internal string BuildCallbackUrl(RestWebhookConfiguration webhookConfiguration)
    {
        if (webhookConfiguration == null)
        {
            throw new ArgumentNullException(nameof(webhookConfiguration));
        }

        var baseUrl = ResponseCallbackBaseUrl.TrimEnd('/');
        return baseUrl + webhookConfiguration.Path;
    }

    internal TimeSpan GetPendingTimeout()
        => TimeSpan.FromSeconds(PendingTimeoutSeconds);

    /// <summary>
    /// Validates AsyncOutbox-specific settings.
    /// </summary>
    public void ValidateAsyncOutbox()
    {
        Validate();
        if (RequestMode != RestRequestReplyRequestMode.AsyncOutbox)
        {
            throw new InvalidOperationException(
                $"Profile '{Name}' must use RequestMode=AsyncOutbox for rpc pending relay.");
        }
    }

    internal Uri? BuildHealthCheckUri()
    {
        if (string.IsNullOrWhiteSpace(HealthCheckPath) || string.IsNullOrWhiteSpace(BaseAddress))
        {
            return null;
        }

        var baseUri = new Uri(BaseAddress.EndsWith("/", StringComparison.Ordinal) ? BaseAddress : BaseAddress + "/");
        var relativePath = HealthCheckPath.StartsWith("/", StringComparison.Ordinal)
            ? HealthCheckPath
            : "/" + HealthCheckPath;
        return new Uri(baseUri, relativePath);
    }

    internal TimeSpan GetResponseTimeout()
        => TimeSpan.FromSeconds(ResponseTimeoutSeconds);

    internal Uri BuildRequestUri()
    {
        var baseUri = new Uri(BaseAddress.EndsWith("/", StringComparison.Ordinal) ? BaseAddress : BaseAddress + "/");
        var relativePath = RequestPath.StartsWith("/", StringComparison.Ordinal)
            ? RequestPath
            : "/" + RequestPath;
        return new Uri(baseUri, relativePath);
    }
}
