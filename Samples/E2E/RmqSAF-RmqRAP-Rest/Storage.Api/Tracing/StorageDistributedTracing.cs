using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Storage.Api.Tracing;

public static class StorageDistributedTracing
{
    private static readonly ActivitySource ActivitySource = new(
        IntegrationFlowTelemetry.StorageActivitySourceName,
        "1.0.0");

    public static Activity? StartIngestActivity(
        string? traceParent,
        string? traceState,
        string messageId,
        string correlationId)
    {
        Activity? activity;
        if (!string.IsNullOrWhiteSpace(traceParent)
            && ActivityContext.TryParse(traceParent, traceState, out var parentContext))
        {
            activity = ActivitySource.StartActivity("storage.ingest", ActivityKind.Consumer, parentContext);
        }
        else
        {
            activity = ActivitySource.StartActivity("storage.ingest", ActivityKind.Consumer);
        }

        activity?.SetTag("messaging.message_id", messageId);
        activity?.SetTag("messaging.correlation_id", correlationId);
        return activity;
    }
}
