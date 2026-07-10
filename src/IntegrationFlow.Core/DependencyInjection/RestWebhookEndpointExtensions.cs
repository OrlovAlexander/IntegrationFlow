#if NET8_0_OR_GREATER
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Auth;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationFlow.DependencyInjection;

/// <summary>
/// ASP.NET Core endpoint mapping for inbound REST webhooks.
/// </summary>
public static class RestWebhookEndpointExtensions
{
    /// <summary>
    /// Maps a webhook endpoint using profile path from rest.json.
    /// </summary>
    public static RouteHandlerBuilder MapIntegrationFlowWebhook(
        this IEndpointRouteBuilder endpoints,
        string profileName,
        Func<RestWebhookReceivedMessage, CancellationToken, Task> handler)
    {
        var configuration = RestWebhookConfigurationLoader.LoadProfile(profileName);
        return endpoints.MapIntegrationFlowWebhook(profileName, configuration.Path, handler);
    }

    /// <summary>
    /// Maps a webhook endpoint at the given path for the REST webhook profile.
    /// </summary>
    public static RouteHandlerBuilder MapIntegrationFlowWebhook(
        this IEndpointRouteBuilder endpoints,
        string profileName,
        string path,
        Func<RestWebhookReceivedMessage, CancellationToken, Task> handler,
        Func<IServiceProvider, IMessageDeduplicationStore?>? createDeduplicationStore = null)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name is required.", nameof(profileName));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var configuration = RestWebhookConfigurationLoader.LoadProfile(profileName);
        var processor = new RestWebhookMessageProcessor();
        var allowedMethods = configuration.AllowedMethods
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return endpoints.MapMethods(
            path,
            allowedMethods,
            async (HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var services = httpContext.RequestServices;
                var logger = services.GetRequiredService<IIntegrationLogger>();
                var metrics = services.GetService<IIntegrationFlowMetrics>();
                var deduplicationStore = createDeduplicationStore?.Invoke(services)
                    ?? services.GetService<IMessageDeduplicationStore>();
                var authenticator = services.GetService<IRestWebhookAuthenticator>();

                var result = await processor.ProcessAsync(
                        httpContext,
                        configuration,
                        handler,
                        logger,
                        metrics,
                        deduplicationStore,
                        authenticator,
                        cancellationToken)
                    .ConfigureAwait(false);

                httpContext.Response.StatusCode = RestWebhookProcessResultMapper.ToStatusCode(result);
            });
    }
}
#endif
