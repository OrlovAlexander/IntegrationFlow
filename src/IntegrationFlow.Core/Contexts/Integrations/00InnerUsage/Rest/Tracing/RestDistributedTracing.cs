#if NET8_0_OR_GREATER
using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Tracing;

internal static class RestDistributedTracing
{
    private static readonly ActivitySource ActivitySource = new(
        IntegrationFlowRestActivitySource.Name,
        IntegrationFlowRestActivitySource.Version);

    public static Activity? StartConsumerActivity(
        IHeaderDictionary headers,
        string operation,
        string profile,
        string? messageId = null,
        string? correlationId = null)
    {
        Activity? activity;
        if (RestHttpTracePropagation.TryExtractParentContext(headers, out var parentContext))
        {
            activity = ActivitySource.StartActivity(
                $"rest.{operation}",
                ActivityKind.Consumer,
                parentContext);
        }
        else
        {
            activity = ActivitySource.StartActivity(
                $"rest.{operation}",
                ActivityKind.Consumer);
        }

        EnrichActivity(activity, operation, profile, messageId, correlationId);
        return activity;
    }

    private static void EnrichActivity(
        Activity? activity,
        string operation,
        string profile,
        string? messageId,
        string? correlationId)
    {
        if (activity == null)
        {
            return;
        }

        activity.SetTag("integrationflow.transport", "rest");
        activity.SetTag("integrationflow.profile", profile);
        activity.SetTag("integrationflow.operation", operation);

        if (!string.IsNullOrWhiteSpace(messageId))
        {
            activity.SetTag("messaging.message_id", messageId);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            activity.SetTag("messaging.correlation_id", correlationId);
        }
    }
}
#endif
