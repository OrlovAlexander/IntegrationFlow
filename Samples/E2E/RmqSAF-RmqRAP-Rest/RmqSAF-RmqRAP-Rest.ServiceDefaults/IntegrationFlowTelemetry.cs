using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class IntegrationFlowTelemetry
{
    public const string RabbitMqActivitySourceName = "IntegrationFlow.RabbitMq";

    public const string RestActivitySourceName = "IntegrationFlow.Rest";

    public const string StorageActivitySourceName = "RmqSAF-RmqRAP-Rest.Storage";

    public static TracerProviderBuilder AddIntegrationFlowTracing(this TracerProviderBuilder tracing)
        => tracing
            .AddSource(RabbitMqActivitySourceName)
            .AddSource(RestActivitySourceName)
            .AddSource(StorageActivitySourceName);
}
