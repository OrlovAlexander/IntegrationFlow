using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Connections;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;

internal static class RabbitMqConnectionPoolsBootstrap
{
    internal static void Configure(
        IIntegrationFlowMetrics metrics,
        RabbitMqTransportHealthRegistry? healthRegistry = null)
    {
        metrics ??= NullIntegrationFlowMetrics.Instance;
        RabbitMqPublishConnectionPool.SetMetrics(metrics);
        RabbitMqRequestReplyConnectionPool.SetMetrics(metrics);
        healthRegistry?.SetMetrics(metrics);
    }
}
