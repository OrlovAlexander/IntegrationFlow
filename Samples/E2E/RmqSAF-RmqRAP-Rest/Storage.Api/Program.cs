using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using RmqSAF_RmqRAP_Rest.Contracts;
using Storage.Api.Storage;
using Storage.Api.Tracing;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<IPayloadStore, InMemoryPayloadStore>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/payloads", (IPayloadStore store, string? correlationId) =>
{
    var items = string.IsNullOrWhiteSpace(correlationId)
        ? store.GetAll()
        : store.GetByCorrelationId(correlationId);

    return Results.Ok(items.Select(ToDto));
});

app.MapGet("/api/payloads/{id}", (string id, IPayloadStore store) =>
{
    var payload = store.GetById(id);
    return payload is null ? Results.NotFound() : Results.Ok(ToDto(payload));
});

app.MapPost("/api/payloads", async (
    HttpRequest request,
    IPayloadStore store,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var correlationId = request.Headers[IntegrationHeaderNames.CorrelationId].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N");
    var messageId = request.Headers[IntegrationHeaderNames.IdempotencyKey].FirstOrDefault()
        ?? correlationId;
    var traceParent = request.Headers[IntegrationHeaderNames.TraceParent].FirstOrDefault();
    var traceState = request.Headers[IntegrationHeaderNames.TraceState].FirstOrDefault();

    using var activity = StorageDistributedTracing.StartIngestActivity(
        traceParent,
        traceState,
        messageId,
        correlationId);

    using (logger.BeginScope(new Dictionary<string, object>
    {
        [IntegrationStructuredLogFields.MessageId] = messageId,
        [IntegrationStructuredLogFields.CorrelationId] = correlationId,
    }))
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var headers = request.Headers.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var payload = new StoredPayload(
            messageId,
            correlationId,
            DateTimeOffset.UtcNow,
            body,
            headers);

        await store.StoreAsync(payload, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("PayloadStored MessageId={MessageId} CorrelationId={CorrelationId}", messageId, correlationId);

        return Results.Created($"/api/payloads/{messageId}", new { messageId, correlationId });
    }
});

app.MapDefaultEndpoints();

app.Run();

static StoredPayloadDto ToDto(StoredPayload payload)
    => new(
        payload.Id,
        payload.CorrelationId,
        payload.ReceivedAt,
        payload.Body,
        payload.SourceHeaders);
