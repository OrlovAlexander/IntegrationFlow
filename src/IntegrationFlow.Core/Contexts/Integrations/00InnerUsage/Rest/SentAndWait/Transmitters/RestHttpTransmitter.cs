using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Auth;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Exceptions;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Cache;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Tracing;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Transmitters;

/// <summary>
/// HTTP request-reply transmitter for SentAndWait.
/// </summary>
internal sealed class RestHttpTransmitter : ITransmitter, ITransmitterAsync, IMetricsAwareTransmitter
{
    private const string RestTransport = "rest";

    private readonly RestRequestReplyConfiguration configuration;
    private readonly RestHttpConnection connection;
    private readonly SemaphoreSlim? concurrencyGate;
    private readonly IRestClientResponseCache? responseCache;

    public RestHttpTransmitter(IConfiguration configuration, RestHttpConnection connection)
    {
        this.configuration = (RestRequestReplyConfiguration)configuration
            ?? throw new ArgumentNullException(nameof(configuration));
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        concurrencyGate = CreateConcurrencyGate(this.configuration.MaxConcurrentRequests);
        responseCache = RestClientResponseCacheRegistry.Instance;
    }

    public IIntegrationFlowMetrics? Metrics { get; set; }

    public ObtainedData Transmit(TransmitData transmitData)
        => TransmitAsync(transmitData, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<ObtainedData> TransmitAsync(TransmitData transmitData, CancellationToken cancellationToken)
    {
        if (concurrencyGate != null)
        {
            await concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        var stopwatch = Stopwatch.StartNew();
        var success = false;
        var timedOut = false;
        try
        {
            configuration.Validate();

            var cached = await TryGetCachedResponseAsync(transmitData, cancellationToken).ConfigureAwait(false);
            if (cached != null)
            {
                success = true;
                return cached.Value;
            }

            var maxAttempts = GetMaxAttempts(transmitData);
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (attempt > 0)
                {
                    Metrics?.RecordRequestReplyRetryAfterTimeout(configuration.Name);
                    await Task.Delay(SentAndWaitIntegrationOptions.RetryDelay, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    var result = await SendRequestOnceAsync(transmitData, cancellationToken).ConfigureAwait(false);
                    success = !result.IsFailed;
                    await StoreCachedResponseAsync(transmitData, result, cancellationToken).ConfigureAwait(false);
                    return result;
                }
                catch (SentAndWaitTimeoutException) when (attempt < maxAttempts - 1 && ShouldRetryOnTimeout(transmitData))
                {
                    continue;
                }
                catch (RestHttpException ex) when (attempt < maxAttempts - 1 && IsTransient(ex))
                {
                    continue;
                }
            }

            throw new InvalidOperationException("REST request did not complete.");
        }
        catch (SentAndWaitTimeoutException)
        {
            timedOut = true;
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            throw new SentAndWaitTimeoutException(
                $"REST request to profile '{configuration.Name}' timed out after {configuration.ResponseTimeoutSeconds}s.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            throw new SentAndWaitTimeoutException(
                $"REST request to profile '{configuration.Name}' timed out after {configuration.ResponseTimeoutSeconds}s.");
        }
        finally
        {
            Metrics?.RecordRequestReply(
                configuration.Name,
                stopwatch.Elapsed,
                success,
                timedOut,
                RestTransport);
            concurrencyGate?.Release();
        }
    }

    private async Task<ObtainedData?> TryGetCachedResponseAsync(
        TransmitData transmitData,
        CancellationToken cancellationToken)
    {
        if (responseCache == null || string.IsNullOrWhiteSpace(transmitData.MessageId))
        {
            return null;
        }

        var cached = await responseCache
            .TryGetAsync(configuration.Name, transmitData.MessageId, cancellationToken)
            .ConfigureAwait(false);
        if (cached == null)
        {
            return null;
        }

        return new ObtainedData(Encoding.UTF8.GetString(cached));
    }

    private async Task StoreCachedResponseAsync(
        TransmitData transmitData,
        ObtainedData result,
        CancellationToken cancellationToken)
    {
        if (responseCache == null ||
            result.IsFailed ||
            string.IsNullOrWhiteSpace(transmitData.MessageId) ||
            result.Data == null)
        {
            return;
        }

        var body = result.Data as string ?? JsonSerializer.Serialize(result.Data);
        await responseCache
            .StoreAsync(
                configuration.Name,
                transmitData.MessageId,
                Encoding.UTF8.GetBytes(body),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ObtainedData> SendRequestOnceAsync(
        TransmitData transmitData,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(configuration.GetResponseTimeout());

        var requestUri = configuration.BuildRequestUri();
        using var request = new HttpRequestMessage(new HttpMethod(configuration.Method), requestUri);
        ApplyHeaders(request, transmitData);
        RestHttpAuthentication.Apply(request, configuration);
        RestTracePropagation.Inject(request.Headers);

        var body = SerializeBody(transmitData.Data);
        if (!string.IsNullOrEmpty(body) && !IsBodylessMethod(configuration.Method))
        {
            request.Content = new StringContent(body, Encoding.UTF8, configuration.ContentType);
        }

        HttpResponseMessage response;
        try
        {
            response = await connection.HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SentAndWaitTimeoutException(
                $"REST request to profile '{configuration.Name}' timed out after {configuration.ResponseTimeoutSeconds}s.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SentAndWaitTimeoutException(
                $"REST request to profile '{configuration.Name}' timed out after {configuration.ResponseTimeoutSeconds}s.");
        }
        catch (HttpRequestException ex)
        {
            throw new RestHttpException($"REST request to '{requestUri}' failed.", innerException: ex);
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrEmpty(responseBody) && response.StatusCode != HttpStatusCode.NoContent)
                {
                    throw new RestHttpException(
                        $"REST request to '{requestUri}' returned empty body with status {statusCode}.",
                        statusCode);
                }

                return new ObtainedData(responseBody ?? string.Empty);
            }

            if (statusCode >= 400 && statusCode < 500)
            {
                return new ObtainedData(responseBody, isFailed: true);
            }

            throw new RestHttpException(
                $"REST request to '{requestUri}' failed with status {statusCode}.",
                statusCode);
        }
    }

    private void ApplyHeaders(HttpRequestMessage request, TransmitData transmitData)
    {
        if (!string.IsNullOrWhiteSpace(configuration.Accept))
        {
            request.Headers.Accept.ParseAdd(configuration.Accept);
        }

        if (!string.IsNullOrWhiteSpace(transmitData.MessageId) &&
            !string.IsNullOrWhiteSpace(configuration.IdempotencyHeaderName))
        {
            request.Headers.TryAddWithoutValidation(configuration.IdempotencyHeaderName, transmitData.MessageId);
        }
    }

    private int GetMaxAttempts(TransmitData transmitData)
    {
        var attempts = 1;
        if (ShouldRetryOnTimeout(transmitData))
        {
            attempts = Math.Max(attempts, 1 + SentAndWaitIntegrationOptions.MaxRetries);
        }

        if (configuration.RetryOnTransientErrors && configuration.MaxTransientRetries > 0)
        {
            attempts = Math.Max(attempts, 1 + configuration.MaxTransientRetries);
        }

        return attempts;
    }

    private static bool ShouldRetryOnTimeout(TransmitData transmitData)
        => SentAndWaitIntegrationOptions.RetryOnTimeout &&
           !string.IsNullOrWhiteSpace(transmitData.MessageId) &&
           SentAndWaitIntegrationOptions.MaxRetries > 0;

    private static bool IsTransient(RestHttpException exception)
    {
        if (exception.StatusCode == null)
        {
            return true;
        }

        var statusCode = exception.StatusCode.Value;
        return statusCode >= 500 || statusCode == 429;
    }

    private static string SerializeBody(object? data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        return data switch
        {
            string text => text,
            _ => JsonSerializer.Serialize(data),
        };
    }

    private static bool IsBodylessMethod(string method)
        => string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);

    private static SemaphoreSlim? CreateConcurrencyGate(int maxConcurrentRequests)
        => maxConcurrentRequests <= 0 ? null : new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
}
