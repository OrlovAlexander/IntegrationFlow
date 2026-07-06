namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Tracing;

/// <summary>
/// ActivitySource name for RabbitMQ distributed tracing. Register in host OpenTelemetry SDK.
/// </summary>
public static class IntegrationFlowRabbitMqActivitySource
{
    public const string Name = "IntegrationFlow.RabbitMq";

    public const string Version = "1.0.0";
}
