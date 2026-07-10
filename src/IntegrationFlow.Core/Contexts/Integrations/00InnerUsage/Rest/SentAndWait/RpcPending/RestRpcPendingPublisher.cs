using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Auth;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Exceptions;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Tracing;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.RpcPending;

internal sealed class RestRpcPendingPublisher : IRpcPendingPublisher
{
    private readonly RestRequestReplyConfiguration configuration;
    private readonly RestWebhookConfiguration responseWebhookConfiguration;
    private readonly RestHttpConnection connection;

    public RestRpcPendingPublisher(
        RestRequestReplyConfiguration configuration,
        RestWebhookConfiguration responseWebhookConfiguration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.responseWebhookConfiguration = responseWebhookConfiguration
            ?? throw new ArgumentNullException(nameof(responseWebhookConfiguration));
        EnsureAsyncOutboxConfiguration(this.configuration);
        connection = new RestHttpConnection(this.configuration);
    }

    public RpcPendingTransportKind TransportKind => RpcPendingTransportKind.Rest;

    public void PublishPendingRequest(RpcPendingRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var maxAttempts = GetMaxAttempts();
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                SendOnce(request);
                return;
            }
            catch (RestHttpClientErrorException)
            {
                throw;
            }
            catch (RestHttpException ex) when (attempt < maxAttempts - 1 && IsTransient(ex))
            {
            }
        }

        throw new InvalidOperationException("REST async outbox publish did not complete.");
    }

    public void Dispose()
    {
        connection.Dispose();
    }

    internal static void EnsureAsyncOutboxConfiguration(RestRequestReplyConfiguration configuration)
        => configuration.ValidateAsyncOutbox();

    private void SendOnce(RpcPendingRequest request)
    {
        using var timeoutCts = new CancellationTokenSource(configuration.GetResponseTimeout());
        var requestUri = configuration.BuildRequestUri();
        using var httpRequest = new HttpRequestMessage(new HttpMethod(configuration.Method), requestUri);
        ApplyHeaders(httpRequest, request);
        RestHttpAuthentication.Apply(httpRequest, configuration);
        RestTracePropagation.Inject(httpRequest.Headers);

        if (request.RequestPayload.Length > 0 && !IsBodylessMethod(configuration.Method))
        {
            httpRequest.Content = new ByteArrayContent(request.RequestPayload);
            httpRequest.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(request.ContentType);
        }

        HttpResponseMessage response;
        try
        {
            response = connection.HttpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException) when (!timeoutCts.IsCancellationRequested)
        {
            throw new RestHttpException($"REST async outbox publish to '{requestUri}' was canceled.");
        }
        catch (OperationCanceledException)
        {
            throw new RestHttpException(
                $"REST async outbox publish to profile '{configuration.Name}' timed out after {configuration.ResponseTimeoutSeconds}s.");
        }
        catch (HttpRequestException ex)
        {
            throw new RestHttpException($"REST async outbox publish to '{requestUri}' failed.", innerException: ex);
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;
            if (configuration.IsAcceptedStatusCode(statusCode))
            {
                return;
            }

            if (statusCode >= 400 && statusCode < 500)
            {
                throw new RestHttpClientErrorException(
                    $"REST async outbox publish to '{requestUri}' failed with status {statusCode}.",
                    statusCode);
            }

            throw new RestHttpException(
                $"REST async outbox publish to '{requestUri}' failed with status {statusCode}.",
                statusCode);
        }
    }

    private void ApplyHeaders(HttpRequestMessage httpRequest, RpcPendingRequest request)
    {
        var pendingId = request.Id.ToString("N");
        var callbackUrl = configuration.BuildCallbackUrl(responseWebhookConfiguration);

        httpRequest.Headers.TryAddWithoutValidation(configuration.IdempotencyHeaderName, pendingId);
        httpRequest.Headers.TryAddWithoutValidation(configuration.CorrelationIdHeaderName, pendingId);
        httpRequest.Headers.TryAddWithoutValidation(configuration.CallbackUrlHeaderName, callbackUrl);

        if (!string.IsNullOrWhiteSpace(configuration.Accept))
        {
            httpRequest.Headers.TryAddWithoutValidation("Accept", configuration.Accept);
        }
    }

    private int GetMaxAttempts()
        => configuration.RetryOnTransientErrors
            ? Math.Max(1, configuration.MaxTransientRetries + 1)
            : 1;

    private static bool IsTransient(RestHttpException exception)
        => exception.StatusCode == null ||
           exception.StatusCode >= 500 ||
           exception.StatusCode == 429;

    private static bool IsBodylessMethod(string method)
        => string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);
}
