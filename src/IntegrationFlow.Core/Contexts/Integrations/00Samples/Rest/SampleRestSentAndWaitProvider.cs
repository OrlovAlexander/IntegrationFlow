using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait;
using IntegrationFlow.Contexts.Integrations._02Application;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;

namespace IntegrationFlow.Contexts.Integrations._00Samples.Rest;

internal sealed class OrdersLookupRestOppositeSide : RestSentAndWaitIntegrationOppositeSideBase
{
    protected override string ConfigurationName => "OrdersLookup";

    protected override object GetIntegrationOppositeSideCode() => "OrdersLookup";
}

internal sealed class SampleRestSentAndWaitResultHandler : SentAndWaitIntegrationResultHandler
{
    public object? LastResult { get; private set; }

    public override void ProcessResult(ObtainedData result)
    {
        LastResult = result.Data;
    }

    public override void ProcessFailedResult(ObtainedData result)
    {
        throw new InvalidOperationException("REST SentAndWait integration returned failed result.");
    }
}

internal sealed class SampleRestSentAndWaitProvider : ISentAndWaitIntegrationOppositeSideProvider
{
    public SentAndWaitIntegrationOppositeSide IntegrationOppositeSideResolve(object integrationOppositeSideCode)
    {
        return integrationOppositeSideCode switch
        {
            "OrdersLookup" => new OrdersLookupRestOppositeSide(),
            _ => throw new InvalidOperationException($"Unknown REST SentAndWait opposite side: {integrationOppositeSideCode}")
        };
    }

    public SentAndWaitIntegrationResultHandler ResultHandlerResolve(object integrationOppositeSideCode)
    {
        return integrationOppositeSideCode switch
        {
            "OrdersLookup" => new SampleRestSentAndWaitResultHandler(),
            _ => throw new InvalidOperationException($"Unknown REST SentAndWait opposite side: {integrationOppositeSideCode}")
        };
    }
}
