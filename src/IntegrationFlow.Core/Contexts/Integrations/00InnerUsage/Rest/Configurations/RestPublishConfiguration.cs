using System;
using System.Linq;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Cfg;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// REST publish configuration for SentAndForgot.
/// </summary>
public sealed class RestPublishConfiguration : IConfiguration, IRestConnectionConfiguration
{
    public string Name { get; set; } = string.Empty;

    public string BaseAddress { get; set; } = string.Empty;

    public string RequestPath { get; set; } = string.Empty;

    public string Method { get; set; } = "POST";

    public string ContentType { get; set; } = "application/json";

    public string Accept { get; set; } = "application/json";

    public int PublishTimeoutSeconds { get; set; } = 30;

    public int TimeoutSeconds { get; set; } = 30;

    public string IdempotencyHeaderName { get; set; } = "Idempotency-Key";

    public int[] ExpectedStatusCodes { get; set; } = { 200, 201, 202, 204 };

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

    public bool RetryOnTransientErrors { get; set; } = true;

    public int MaxTransientRetries { get; set; } = 1;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseAddress))
        {
            throw new InvalidOperationException("BaseAddress is required for REST publish.");
        }

        if (string.IsNullOrWhiteSpace(RequestPath))
        {
            throw new InvalidOperationException("RequestPath is required for REST publish.");
        }

        if (string.IsNullOrWhiteSpace(Method))
        {
            throw new InvalidOperationException("Method is required for REST publish.");
        }

        if (PublishTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("PublishTimeoutSeconds must be greater than 0.");
        }

        if (ExpectedStatusCodes == null || ExpectedStatusCodes.Length == 0)
        {
            throw new InvalidOperationException("ExpectedStatusCodes must contain at least one HTTP status code.");
        }

        if (MaxTransientRetries < 0)
        {
            throw new InvalidOperationException("MaxTransientRetries must be greater than or equal to 0.");
        }
    }

    internal TimeSpan GetPublishTimeout()
        => TimeSpan.FromSeconds(PublishTimeoutSeconds);

    internal Uri BuildRequestUri()
    {
        var baseUri = new Uri(BaseAddress.EndsWith("/", StringComparison.Ordinal) ? BaseAddress : BaseAddress + "/");
        var relativePath = RequestPath.StartsWith("/", StringComparison.Ordinal)
            ? RequestPath
            : "/" + RequestPath;
        return new Uri(baseUri, relativePath);
    }

    internal bool IsExpectedStatusCode(int statusCode)
        => ExpectedStatusCodes.Contains(statusCode);
}
