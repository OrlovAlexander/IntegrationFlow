using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Auth;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Exceptions;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Tracing;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndForgot.Transmitters;

/// <summary>
/// HTTP publish transmitter for SentAndForgot.
/// </summary>
internal sealed class RestPublishTransmitter : ITransmitter, ITransmitterWithResult
{
    private readonly RestPublishConfiguration configuration;
    private readonly RestPublishConnection connection;

    public RestPublishTransmitter(RestPublishConfiguration configuration, RestPublishConnection connection)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public void Transmit(TransmitData transmitData)
        => TransmitWithResult(transmitData);

    public TransmitResult TransmitWithResult(TransmitData transmitData)
    {
        configuration.Validate();

        var maxAttempts = GetMaxAttempts();
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return SendOnce(transmitData);
            }
            catch (RestHttpClientErrorException)
            {
                throw;
            }
            catch (RestHttpException ex) when (attempt < maxAttempts - 1 && IsTransient(ex))
            {
            }
        }

        throw new InvalidOperationException("REST publish did not complete.");
    }

    private TransmitResult SendOnce(TransmitData transmitData)
    {
        var messageId = ResolveMessageId(transmitData);
        using var timeoutCts = new CancellationTokenSource(configuration.GetPublishTimeout());

        var requestUri = configuration.BuildRequestUri();
        using var request = new HttpRequestMessage(new HttpMethod(configuration.Method), requestUri);
        ApplyHeaders(request, transmitData, messageId);
        RestHttpAuthentication.Apply(request, configuration);
        RestTracePropagation.Inject(request.Headers);

        var body = IntegrationPayloadSerializer.SerializeToBytes(transmitData.Data);
        if (body.Length > 0 && !IsBodylessMethod(configuration.Method))
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(configuration.ContentType);
        }

        HttpResponseMessage response;
        try
        {
            response = connection.HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException) when (!timeoutCts.IsCancellationRequested)
        {
            throw new RestHttpException($"REST publish to '{requestUri}' was canceled.");
        }
        catch (OperationCanceledException)
        {
            throw new RestHttpException(
                $"REST publish to profile '{configuration.Name}' timed out after {configuration.PublishTimeoutSeconds}s.");
        }
        catch (HttpRequestException ex)
        {
            throw new RestHttpException($"REST publish to '{requestUri}' failed.", innerException: ex);
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;
            if (configuration.IsExpectedStatusCode(statusCode))
            {
                return TransmitResult.Create(messageId);
            }

            if (statusCode >= 400 && statusCode < 500)
            {
                throw new RestHttpClientErrorException(
                    $"REST publish to '{requestUri}' failed with status {statusCode}.",
                    statusCode);
            }

            throw new RestHttpException(
                $"REST publish to '{requestUri}' failed with status {statusCode}.",
                statusCode);
        }
    }

    private static string ResolveMessageId(TransmitData transmitData)
        => string.IsNullOrWhiteSpace(transmitData.MessageId)
            ? Guid.NewGuid().ToString("N")
            : transmitData.MessageId;

    private void ApplyHeaders(HttpRequestMessage request, TransmitData transmitData, string messageId)
    {
        if (!string.IsNullOrWhiteSpace(configuration.Accept))
        {
            request.Headers.Accept.ParseAdd(configuration.Accept);
        }

        if (!string.IsNullOrWhiteSpace(configuration.IdempotencyHeaderName))
        {
            request.Headers.TryAddWithoutValidation(configuration.IdempotencyHeaderName, messageId);
        }

        if (!string.IsNullOrWhiteSpace(transmitData.CorrelationId))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", transmitData.CorrelationId);
        }
    }

    private int GetMaxAttempts()
    {
        if (!configuration.RetryOnTransientErrors || configuration.MaxTransientRetries <= 0)
        {
            return 1;
        }

        return 1 + configuration.MaxTransientRetries;
    }

    private static bool IsTransient(RestHttpException exception)
    {
        if (exception.StatusCode == null)
        {
            return true;
        }

        var statusCode = exception.StatusCode.Value;
        return statusCode >= 500 || statusCode == 429;
    }

    private static bool IsBodylessMethod(string method)
        => string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);
}
