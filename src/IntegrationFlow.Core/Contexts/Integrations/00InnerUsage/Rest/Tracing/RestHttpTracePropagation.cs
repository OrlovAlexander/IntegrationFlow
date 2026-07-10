#if NET8_0_OR_GREATER
using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Tracing;

internal static class RestHttpTracePropagation
{
    public static bool TryExtractParentContext(IHeaderDictionary headers, out ActivityContext parentContext)
    {
        parentContext = default;
        if (headers == null || headers.Count == 0)
        {
            return false;
        }

        if (!headers.TryGetValue(RestTracePropagation.TraceParentHeader, out var traceParentValues))
        {
            return false;
        }

        var traceParent = traceParentValues.ToString();
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return false;
        }

        string? traceState = null;
        if (headers.TryGetValue(RestTracePropagation.TraceStateHeader, out var traceStateValues))
        {
            traceState = traceStateValues.ToString();
        }

        return ActivityContext.TryParse(traceParent, traceState, out parentContext);
    }
}
#endif
