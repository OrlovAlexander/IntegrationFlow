using IntegrationFlow.DependencyInjection;
using IntegrationFlow.Metrics.OpenTelemetry.DependencyInjection;
using RmqSAF_RmqRAP_Rest.Contracts;
using Sender.Api.Application.SendPayload;
using Sender.Api.Domain;
using Sender.Api.Infrastructure.IntegrationFlow;

var builder = WebApplication.CreateBuilder(args);

RabbitMqAspireConfiguration.ApplyRabbitMqConnectionString(
    builder.Configuration,
    ("RabbitMqPublish", "E2EOut"));

builder.AddServiceDefaults();

builder.Services.AddIntegrationFlow();
builder.Services.AddIntegrationFlowOpenTelemetryMetrics();
builder.Services.AddIntegrationFlowRabbitMq(builder.Configuration);

builder.Services.AddSingleton<IIntegrationPublisher, RabbitMqSentAndForgotGateway>();
builder.Services.AddSingleton<SendPayloadCommandHandler>();

builder.Services.AddIntegrationFlowRabbitMqHealthChecks();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/messages", (SendPayloadRequest request, SendPayloadCommandHandler handler, CancellationToken ct) =>
{
    var response = handler.Handle(request, ct);
    return Results.Accepted("/api/messages", response);
});

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
