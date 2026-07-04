using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait;
using IntegrationFlow.Contexts.Integrations._02Application;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;

namespace IntegrationFlow.Contexts.Integrations._00Samples.SentAndWait
{
    internal sealed class OrdersRpcRabbitMqOppositeSide : RabbitMqSentAndWaitIntegrationOppositeSideBase
    {
        protected override string ConfigurationName => "OrdersRpc";

        protected override object GetIntegrationOppositeSideCode() => "OrdersRpc";
    }

    internal sealed class SampleRabbitMqSentAndWaitResultHandler : SentAndWaitIntegrationResultHandler
    {
        public object LastResult { get; private set; }

        public override void ProcessResult(ObtainedData result)
        {
            LastResult = result.Data;
        }

        public override void ProcessFailedResult(ObtainedData result)
        {
            throw new InvalidOperationException("SentAndWait integration returned failed result.");
        }
    }

    internal sealed class SampleRabbitMqSentAndWaitProvider : ISentAndWaitIntegrationOppositeSideProvider
    {
        public SentAndWaitIntegrationOppositeSide IntegrationOppositeSideResolve(object integrationOppositeSideCode)
        {
            return integrationOppositeSideCode switch
            {
                "OrdersRpc" => new OrdersRpcRabbitMqOppositeSide(),
                _ => throw new InvalidOperationException($"Unknown SentAndWait opposite side: {integrationOppositeSideCode}")
            };
        }

        public SentAndWaitIntegrationResultHandler ResultHandlerResolve(object integrationOppositeSideCode)
        {
            return integrationOppositeSideCode switch
            {
                "OrdersRpc" => new SampleRabbitMqSentAndWaitResultHandler(),
                _ => throw new InvalidOperationException($"Unknown SentAndWait opposite side: {integrationOppositeSideCode}")
            };
        }
    }
}
