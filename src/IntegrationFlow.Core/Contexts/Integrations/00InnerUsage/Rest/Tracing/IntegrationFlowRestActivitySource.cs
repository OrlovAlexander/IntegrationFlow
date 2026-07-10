namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Tracing;

/// <summary>
/// ActivitySource name for REST distributed tracing. Register in host OpenTelemetry SDK.
/// </summary>
public static class IntegrationFlowRestActivitySource
{
    public const string Name = "IntegrationFlow.Rest";

    public const string Version = "1.0.0";
}
