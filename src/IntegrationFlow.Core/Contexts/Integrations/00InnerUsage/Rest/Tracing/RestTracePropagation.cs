using System;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Tracing;

internal static class RestTracePropagation
{
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    public static void Inject(HttpRequestHeaders headers)
    {
        if (headers == null)
        {
            throw new ArgumentNullException(nameof(headers));
        }

        var activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        headers.TryAddWithoutValidation(TraceParentHeader, BuildTraceParent(activity));
        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            headers.TryAddWithoutValidation(TraceStateHeader, activity.TraceStateString);
        }
    }

    private static string BuildTraceParent(Activity activity)
    {
        var flags = activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00";
        return $"00-{activity.TraceId}-{activity.SpanId}-{flags}";
    }
}
