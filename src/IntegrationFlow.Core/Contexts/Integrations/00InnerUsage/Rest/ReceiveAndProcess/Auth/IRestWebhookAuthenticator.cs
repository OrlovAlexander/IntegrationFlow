#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using Microsoft.AspNetCore.Http;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Auth;

/// <summary>
/// Optional hook for webhook authentication or HMAC signature verification (app-provided).
/// </summary>
public interface IRestWebhookAuthenticator
{
    /// <summary>
    /// Returns <c>true</c> when the request is authenticated and may be processed.
    /// </summary>
    Task<bool> TryAuthenticateAsync(
        HttpContext httpContext,
        RestWebhookConfiguration configuration,
        RestWebhookReceivedMessage message,
        CancellationToken cancellationToken = default);
}
#endif
