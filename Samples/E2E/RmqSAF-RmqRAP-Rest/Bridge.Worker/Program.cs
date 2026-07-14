using Bridge.Worker.Application;
using IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.DependencyInjection;
using IntegrationFlow.Metrics.OpenTelemetry.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(options => options.ServicesStartConcurrently = true);

RabbitMqAspireConfiguration.ApplyRabbitMqConnectionString(
    builder.Configuration,
    ("RabbitMq", "E2EInbox"));

RestAspireConfiguration.ApplyRestServiceReference(
    builder.Configuration,
    "storage",
    "StorageApi");

builder.AddServiceDefaults();

builder.Services.AddIntegrationFlow();
builder.Services.AddIntegrationFlowOpenTelemetryMetrics();
builder.Services.AddIntegrationFlowRabbitMq(builder.Configuration);
builder.Services.AddIntegrationFlowRest(builder.Configuration);

builder.Services.AddSingleton<ForwardPayloadHandler>();
builder.Services.AddSingleton<InMemoryMessageDeduplicationStore>();

builder.Services.AddIntegrationFlowRabbitMqListener(
    "E2EInbox",
    sp => new DelegateInboxMessageProcessing(inboxMessage =>
    {
        var handler = sp.GetRequiredService<ForwardPayloadHandler>();
        handler.HandleAsync(inboxMessage, CancellationToken.None).GetAwaiter().GetResult();
    }),
    sp => sp.GetRequiredService<InMemoryMessageDeduplicationStore>());

builder.Services.AddIntegrationFlowRabbitMqHealthChecks();
builder.Services.AddIntegrationFlowRestHealthChecks();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
