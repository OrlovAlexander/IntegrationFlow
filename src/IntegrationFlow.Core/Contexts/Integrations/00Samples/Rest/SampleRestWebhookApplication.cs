#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationFlow.Contexts.Integrations._00Samples.Rest;

/// <summary>
/// Sample ASP.NET Core wiring for inbound REST webhooks.
/// </summary>
public static class SampleRestWebhookApplication
{
    /// <summary>
    /// Registers REST transport and maps the OrdersInbox webhook endpoint.
    /// </summary>
    public static WebApplication ConfigureSampleRestWebhookApp(WebApplicationBuilder builder)
    {
        builder.Services.AddIntegrationFlowRest(builder.Configuration);
        builder.Services.AddSingleton<IIntegrationLogger>(_ => NullIntegrationLogger.Instance);

        var app = builder.Build();

        app.MapIntegrationFlowWebhook(
            "OrdersInbox",
            "/integrations/webhooks/orders",
            HandleOrdersWebhookAsync);

        return app;
    }

    private static Task HandleOrdersWebhookAsync(RestWebhookReceivedMessage message, CancellationToken cancellationToken)
    {
        // Business handler: parse message.BodyText and persist/update domain state.
        return Task.CompletedTask;
    }
}
#endif
