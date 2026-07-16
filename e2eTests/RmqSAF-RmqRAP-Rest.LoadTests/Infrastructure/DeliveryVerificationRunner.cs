using RmqSAF_RmqRAP_Rest.LoadTests.Configuration;

namespace RmqSAF_RmqRAP_Rest.LoadTests.Infrastructure;

public sealed record DeliveryVerificationResult(
    int TrackedCount,
    int DeliveredCount,
    double SuccessRate,
    bool Passed);

public static class DeliveryVerificationRunner
{
    public static async Task<DeliveryVerificationResult> VerifyAsync(
        LoadTestSettings settings,
        CorrelationTracker tracker,
        CancellationToken cancellationToken)
    {
        var correlationIds = tracker.Snapshot();
        if (correlationIds.Count == 0)
        {
            return new DeliveryVerificationResult(0, 0, 1, true);
        }

        Console.WriteLine();
        Console.WriteLine($"Waiting {settings.DeliveryVerificationDelaySeconds}s for pipeline drain before delivery verification...");
        await Task.Delay(TimeSpan.FromSeconds(settings.DeliveryVerificationDelaySeconds), cancellationToken)
            .ConfigureAwait(false);

        using var senderClient = CreateClient(settings.SenderBaseUrl, settings.HttpTimeoutSeconds);
        using var storageClient = CreateClient(settings.StorageBaseUrl, settings.HttpTimeoutSeconds);
        using var apiClient = new E2eApiClient(
            senderClient,
            storageClient,
            settings.EventType,
            settings.StoragePollIntervalMs,
            settings.StoragePollTimeoutMs);

        var delivered = 0;
        foreach (var correlationId in correlationIds)
        {
            if (await apiClient.HasPayloadAsync(correlationId, cancellationToken).ConfigureAwait(false))
            {
                delivered++;
            }
        }

        var successRate = delivered / (double)correlationIds.Count;
        var passed = successRate >= settings.MinDeliverySuccessRate;

        return new DeliveryVerificationResult(correlationIds.Count, delivered, successRate, passed);
    }

    private static HttpClient CreateClient(string baseUrl, int timeoutSeconds)
        => new()
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
        };
}
