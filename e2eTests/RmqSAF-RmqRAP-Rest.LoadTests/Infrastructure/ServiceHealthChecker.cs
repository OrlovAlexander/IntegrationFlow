namespace RmqSAF_RmqRAP_Rest.LoadTests.Infrastructure;

public static class ServiceHealthChecker
{
    public static async Task EnsureReadyAsync(
        string senderBaseUrl,
        string storageBaseUrl,
        CancellationToken cancellationToken)
    {
        using var senderClient = CreateClient(senderBaseUrl);
        using var storageClient = CreateClient(storageBaseUrl);

        var senderReady = await WaitForHealthAsync(senderClient, "/alive", cancellationToken).ConfigureAwait(false);
        if (!senderReady)
        {
            throw new InvalidOperationException($"Sender is not reachable at {senderBaseUrl}. Start the E2E stack first.");
        }

        var storageReady = await WaitForHealthAsync(storageClient, "/health", cancellationToken).ConfigureAwait(false);
        if (!storageReady)
        {
            throw new InvalidOperationException(
                $"Storage is not reachable at {storageBaseUrl}. " +
                "For Docker Compose use docker-compose.loadtests.yml to expose storage on port 8081.");
        }
    }

    private static async Task<bool> WaitForHealthAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static HttpClient CreateClient(string baseUrl)
        => new()
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(10),
        };
}
