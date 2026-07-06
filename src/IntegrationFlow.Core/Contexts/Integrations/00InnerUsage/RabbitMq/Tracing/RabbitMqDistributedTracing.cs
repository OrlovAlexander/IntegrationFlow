using System;
using System.Collections.Generic;
using System.Diagnostics;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;

internal static class RabbitMqDistributedTracing
{
    private static readonly ActivitySource ActivitySource = new(
        IntegrationFlowRabbitMqActivitySource.Name,
        IntegrationFlowRabbitMqActivitySource.Version);

    public static Activity? StartProducerActivity(
        string operation,
        string profile,
        string? destination = null,
        string? messageId = null,
        string? correlationId = null)
    {
        var activity = ActivitySource.StartActivity(
            $"rabbitmq.{operation}",
            ActivityKind.Producer);

        EnrichActivity(activity, operation, profile, destination, messageId, correlationId);
        return activity;
    }

    public static Activity? StartConsumerActivity(
        IDictionary<string, object>? headers,
        string operation,
        string profile,
        string? messageId = null,
        string? correlationId = null,
        ulong? deliveryTag = null)
        => StartConsumerActivity(RabbitMqMessageHeaders.Snapshot(headers), operation, profile, messageId, correlationId, deliveryTag);

    public static Activity? StartConsumerActivity(
        IReadOnlyDictionary<string, object>? headers,
        string operation,
        string profile,
        string? messageId = null,
        string? correlationId = null,
        ulong? deliveryTag = null)
    {
        Activity? activity;
        if (RabbitMqTracePropagation.TryExtractParentContext(headers, out var parentContext))
        {
            activity = ActivitySource.StartActivity(
                $"rabbitmq.{operation}",
                ActivityKind.Consumer,
                parentContext);
        }
        else
        {
            activity = ActivitySource.StartActivity(
                $"rabbitmq.{operation}",
                ActivityKind.Consumer);
        }

        EnrichActivity(activity, operation, profile, destination: null, messageId, correlationId);
        if (deliveryTag.HasValue)
        {
            activity?.SetTag("messaging.rabbitmq.delivery_tag", deliveryTag.Value);
        }

        return activity;
    }

    private static void EnrichActivity(
        Activity? activity,
        string operation,
        string profile,
        string? destination,
        string? messageId,
        string? correlationId)
    {
        if (activity == null)
        {
            return;
        }

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation", operation);
        activity.SetTag("integrationflow.profile", profile);

        if (!string.IsNullOrWhiteSpace(destination))
        {
            activity.SetTag("messaging.destination", destination);
        }

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
