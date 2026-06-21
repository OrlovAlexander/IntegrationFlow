using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._02Application;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;

namespace IntegrationFlow.Contexts.Integrations._00Samples.SentAndForgot
{
    internal sealed class OrdersOutRabbitMqOppositeSide : RabbitMqSentAndForgotIntegrationOppositeSideBase
    {
        protected override string ConfigurationName => "OrdersOut";

        protected override object GetIntegrationOppositeSideCode() => "OrdersOut";
    }

    internal sealed class EventsOutRabbitMqOppositeSide : RabbitMqSentAndForgotIntegrationOppositeSideBase
    {
        protected override string ConfigurationName => "EventsOut";

        protected override object GetIntegrationOppositeSideCode() => "EventsOut";
    }

    internal sealed class SampleRabbitMqSentAndForgotProvider : ISentAndForgotIntegrationOppositeSideProvider
    {
        public SentAndForgotIntegrationOppositeSide IntegrationOppositeSideResolve(object integrationOppositeSideCode)
        {
            return integrationOppositeSideCode switch
            {
                "OrdersOut" => new OrdersOutRabbitMqOppositeSide(),
                "EventsOut" => new EventsOutRabbitMqOppositeSide(),
                _ => throw new InvalidOperationException($"Unknown SentAndForgot opposite side: {integrationOppositeSideCode}")
            };
        }
    }
}
