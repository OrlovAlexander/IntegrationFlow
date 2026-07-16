using System.Net.Http.Json;
using System.Text.Json;
using RmqSAF_RmqRAP_Rest.Contracts;

namespace RmqSAF_RmqRAP_Rest.LoadTests.Infrastructure;

public sealed record PublishAttemptResult(
    bool Success,
    string? MessageId,
    string? CorrelationId,
    int PayloadSizeBytes,
    string? Error);

public sealed class E2eApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient senderClient;
    private readonly HttpClient storageClient;
    private readonly string eventType;
    private readonly int storagePollIntervalMs;
    private readonly int storagePollTimeoutMs;

    public E2eApiClient(
        HttpClient senderClient,
        HttpClient storageClient,
        string eventType,
        int storagePollIntervalMs,
        int storagePollTimeoutMs)
    {
        this.senderClient = senderClient;
        this.storageClient = storageClient;
        this.eventType = eventType;
        this.storagePollIntervalMs = storagePollIntervalMs;
        this.storagePollTimeoutMs = storagePollTimeoutMs;
    }

    public async Task<PublishAttemptResult> PublishAsync(long invocationNumber, string instanceId, CancellationToken cancellationToken)
    {
        var payload = new
        {
            type = eventType,
            data = new
            {
                invocation = invocationNumber,
                instanceId,
                sentAt = DateTimeOffset.UtcNow,
            },
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var response = await senderClient.PostAsync("/api/messages", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new PublishAttemptResult(false, null, null, json.Length, $"HTTP {(int)response.StatusCode}: {body}");
        }

        var sendResponse = JsonSerializer.Deserialize<SendPayloadResponse>(body, JsonOptions);
        if (sendResponse is null || string.IsNullOrWhiteSpace(sendResponse.CorrelationId))
        {
            return new PublishAttemptResult(false, null, null, json.Length, "Invalid sender response.");
        }

        return new PublishAttemptResult(
            true,
            sendResponse.MessageId,
            sendResponse.CorrelationId,
            json.Length,
            null);
    }

    public async Task<bool> WaitForDeliveryAsync(string correlationId, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(storagePollTimeoutMs);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await HasPayloadAsync(correlationId, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(storagePollIntervalMs, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public async Task<bool> HasPayloadAsync(string correlationId, CancellationToken cancellationToken)
    {
        using var response = await storageClient
            .GetAsync($"/api/payloads?correlationId={Uri.EscapeDataString(correlationId)}", cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var payloads = await response.Content.ReadFromJsonAsync<List<StoredPayloadDto>>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return payloads is { Count: > 0 };
    }

    public void Dispose()
    {
        senderClient.Dispose();
        storageClient.Dispose();
    }
}
