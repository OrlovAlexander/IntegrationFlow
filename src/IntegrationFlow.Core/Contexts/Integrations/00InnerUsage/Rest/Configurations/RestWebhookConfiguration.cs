using System;
using System.Linq;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// REST inbound webhook profile configuration.
/// </summary>
public sealed class RestWebhookConfiguration : IConfiguration
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint path (for example <c>/integrations/webhooks/orders</c>).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Header used as message id for deduplication.
    /// </summary>
    public string MessageIdHeaderName { get; set; } = "X-Webhook-Id";

    /// <summary>
    /// Optional correlation id header.
    /// </summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-Id";

    /// <summary>
    /// Maximum allowed request body size in bytes.
    /// </summary>
    public int MaxBodyBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Allowed HTTP methods for the webhook endpoint.
    /// </summary>
    public string[] AllowedMethods { get; set; } = { "POST" };

    /// <summary>
    /// When <c>true</c>, requests without message id header are rejected with 400.
    /// </summary>
    public bool RequireMessageId { get; set; }

    /// <inheritdoc />
    public bool Asynchronously { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            throw new InvalidOperationException("Path is required for REST webhook profile.");
        }

        if (!Path.StartsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Webhook Path must start with '/'.");
        }

        if (string.IsNullOrWhiteSpace(MessageIdHeaderName))
        {
            throw new InvalidOperationException("MessageIdHeaderName is required for REST webhook profile.");
        }

        if (MaxBodyBytes <= 0)
        {
            throw new InvalidOperationException("MaxBodyBytes must be greater than 0.");
        }

        if (AllowedMethods == null || AllowedMethods.Length == 0)
        {
            throw new InvalidOperationException("AllowedMethods must contain at least one HTTP method.");
        }

        if (AllowedMethods.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("AllowedMethods must not contain empty values.");
        }
    }

    internal bool IsMethodAllowed(string httpMethod)
    {
        if (string.IsNullOrWhiteSpace(httpMethod))
        {
            return false;
        }

        return AllowedMethods.Any(method =>
            string.Equals(method, httpMethod, StringComparison.OrdinalIgnoreCase));
    }
}
