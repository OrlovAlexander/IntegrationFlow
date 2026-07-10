using System;
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
