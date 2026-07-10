#if NET8_0_OR_GREATER
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Auth;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationFlow.DependencyInjection;

/// <summary>
/// ASP.NET Core endpoint mapping for REST async RPC response callbacks.
/// </summary>
public static class RestRpcResponseEndpointExtensions
{
    /// <summary>
    /// Maps callback webhook endpoint for AsyncOutbox REST request-reply profile.
    /// </summary>
    public static RouteHandlerBuilder MapIntegrationFlowRpcResponseWebhook(
        this IEndpointRouteBuilder endpoints,
        string requestReplyProfileName)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        if (string.IsNullOrWhiteSpace(requestReplyProfileName))
        {
            throw new ArgumentException("Request-reply profile name is required.", nameof(requestReplyProfileName));
        }

        var requestReplyConfiguration = RestRequestReplyConfigurationLoader.LoadProfile(requestReplyProfileName);
        requestReplyConfiguration.ValidateAsyncOutbox();

        var webhookConfiguration = RestWebhookConfigurationLoader.LoadProfile(
            requestReplyConfiguration.ResponseWebhookProfileName);

        return MapIntegrationFlowRpcResponseWebhook(
            endpoints,
            requestReplyConfiguration,
            webhookConfiguration);
    }

    /// <summary>
    /// Maps callback webhook endpoint using explicit request-reply and webhook profiles.
    /// </summary>
    public static RouteHandlerBuilder MapIntegrationFlowRpcResponseWebhook(
        this IEndpointRouteBuilder endpoints,
        RestRequestReplyConfiguration requestReplyConfiguration,
        RestWebhookConfiguration webhookConfiguration)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        if (requestReplyConfiguration == null)
        {
            throw new ArgumentNullException(nameof(requestReplyConfiguration));
        }

        if (webhookConfiguration == null)
        {
            throw new ArgumentNullException(nameof(webhookConfiguration));
        }

        requestReplyConfiguration.ValidateAsyncOutbox();
        webhookConfiguration.Validate();

        var webhookProcessor = new RestWebhookMessageProcessor();
        var correlationProcessor = new RestRpcResponseCorrelationProcessor();
        var allowedMethods = webhookConfiguration.AllowedMethods
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return endpoints.MapMethods(
            webhookConfiguration.Path,
            allowedMethods,
            async (HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var services = httpContext.RequestServices;
                var logger = services.GetRequiredService<IIntegrationLogger>();
                var metrics = services.GetService<IIntegrationFlowMetrics>();
                var pendingStore = services.GetRequiredService<IRpcPendingStore>();
                var deduplicationStore = services.GetService<IMessageDeduplicationStore>();
                var authenticator = services.GetService<IRestWebhookAuthenticator>();

                RestRpcResponseProcessResult? correlationResult = null;

                var webhookResult = await webhookProcessor.ProcessAsync(
                        httpContext,
                        webhookConfiguration,
                        async (message, ct) =>
                        {
                            correlationResult = await correlationProcessor.ProcessAsync(
                                    message,
                                    requestReplyConfiguration,
                                    pendingStore,
                                    logger,
                                    metrics,
                                    ct)
                                .ConfigureAwait(false);
                        },
                        logger,
                        metrics,
                        deduplicationStore: null,
                        authenticator,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (webhookResult != RestWebhookProcessResult.Success)
                {
                    httpContext.Response.StatusCode = RestWebhookProcessResultMapper.ToStatusCode(webhookResult);
                    return;
                }

                httpContext.Response.StatusCode = RestRpcResponseProcessResultMapper.ToStatusCode(
                    correlationResult ?? RestRpcResponseProcessResult.HandlerFailed);
            });
    }
}
#endif
