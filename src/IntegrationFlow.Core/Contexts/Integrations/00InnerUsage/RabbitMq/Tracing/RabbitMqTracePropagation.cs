using System;
using System.Collections.Generic;
using System.Diagnostics;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;

internal static class RabbitMqTracePropagation
{
    public static void Inject(IBasicProperties properties)
    {
        if (properties == null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        var activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        properties.Headers ??= new Dictionary<string, object>();
        properties.Headers[RabbitMqTraceHeaders.TraceParent] = BuildTraceParent(activity);
        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            properties.Headers[RabbitMqTraceHeaders.TraceState] = activity.TraceStateString;
        }
    }

    public static bool TryExtractParentContext(
        IDictionary<string, object>? headers,
        out ActivityContext parentContext)
        => TryExtractParentContext(RabbitMqMessageHeaders.Snapshot(headers), out parentContext);

    public static bool TryExtractParentContext(
        IReadOnlyDictionary<string, object>? headers,
        out ActivityContext parentContext)
    {
        parentContext = default;
        if (headers == null || headers.Count == 0)
        {
            return false;
        }

        if (!RabbitMqMessageHeaders.TryGetString(headers, RabbitMqTraceHeaders.TraceParent, out var traceParent))
        {
            return false;
        }

        RabbitMqMessageHeaders.TryGetString(headers, RabbitMqTraceHeaders.TraceState, out var traceState);

#if NET8_0_OR_GREATER
        return ActivityContext.TryParse(traceParent, traceState, out parentContext);
#else
        return false;
#endif
    }

    internal static string BuildTraceParent(Activity activity)
    {
        var flags = activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00";
        return $"00-{activity.TraceId}-{activity.SpanId}-{flags}";
    }

    internal static bool TryGetStringHeader(IReadOnlyDictionary<string, object> headers, string key, out string value)
        => RabbitMqMessageHeaders.TryGetString(headers, key, out value);
}
