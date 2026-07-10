using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._02Application;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;

namespace IntegrationFlow.Contexts.Integrations._00Samples.Rest;

internal sealed class NotifyWebhookRestOppositeSide : RestSentAndForgotIntegrationOppositeSideBase
{
    protected override string ConfigurationName => "NotifyWebhook";

    protected override object GetIntegrationOppositeSideCode() => "NotifyWebhook";
}

internal sealed class SampleRestSentAndForgotProvider : ISentAndForgotIntegrationOppositeSideProvider
{
    public SentAndForgotIntegrationOppositeSide IntegrationOppositeSideResolve(object integrationOppositeSideCode)
    {
        return integrationOppositeSideCode switch
        {
            "NotifyWebhook" => new NotifyWebhookRestOppositeSide(),
            _ => throw new InvalidOperationException($"Unknown REST SentAndForgot opposite side: {integrationOppositeSideCode}")
        };
    }
}
