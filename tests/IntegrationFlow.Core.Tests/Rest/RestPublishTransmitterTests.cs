using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Exceptions;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using Xunit;

namespace IntegrationFlow.Core.Tests.Rest;

[Collection("RestHttp")]
public sealed class RestPublishTransmitterTests : IDisposable
{
    public void Dispose()
    {
        RestHttpClientProvider.Reset();
    }

    [Fact]
    public void TransmitWithResult_ReturnsMessageIdOn202()
    {
        var configuration = CreateConfiguration();
        configuration.ExpectedStatusCodes = new[] { 200, 202, 204 };
        var transmitter = CreateTransmitter(configuration, (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain"),
            }));

        var result = transmitter.TransmitWithResult(new TransmitData("{\"event\":\"created\"}", "msg-1"));

        Assert.Equal("msg-1", result.MessageId);
    }

    [Fact]
    public void TransmitWithResult_SetsIdempotencyHeaderFromMessageId()
    {
        string? idempotencyHeader = null;
        var configuration = CreateConfiguration();
        var transmitter = CreateTransmitter(configuration, (request, _) =>
        {
            idempotencyHeader = request.Headers.TryGetValues("Idempotency-Key", out var values)
                ? string.Join(",", values)
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain"),
            });
        });

        transmitter.TransmitWithResult(new TransmitData("payload", "stable-key"));

        Assert.Equal("stable-key", idempotencyHeader);
    }

    [Fact]
    public void TransmitWithResult_ThrowsClientErrorOn4xx()
    {
        var configuration = CreateConfiguration();
        configuration.RetryOnTransientErrors = false;
        var transmitter = CreateTransmitter(configuration, (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad", Encoding.UTF8, "text/plain"),
            }));

        var exception = Assert.Throws<RestHttpClientErrorException>(() =>
            transmitter.TransmitWithResult(new TransmitData("payload")));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void TransmitWithResult_RetriesTransient503()
    {
        var attempts = 0;
        var configuration = CreateConfiguration();
        configuration.MaxTransientRetries = 1;
        var transmitter = CreateTransmitter(configuration, (_, _) =>
        {
            attempts++;
            if (attempts == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("busy", Encoding.UTF8, "text/plain"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain"),
            });
        });

        var result = transmitter.TransmitWithResult(new TransmitData("payload", "retry-key"));

        Assert.Equal("retry-key", result.MessageId);
        Assert.Equal(2, attempts);
    }

    private static RestPublishConfiguration CreateConfiguration()
        => new()
        {
            Name = $"PublishProfile-{Guid.NewGuid():N}",
            BaseAddress = "https://api.example.com/",
            RequestPath = "/v1/events",
            Method = "POST",
            PublishTimeoutSeconds = 30,
            RetryOnTransientErrors = true,
            MaxTransientRetries = 0,
        };

    private static RestPublishTransmitter CreateTransmitter(
        RestPublishConfiguration configuration,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        RestHttpClientProvider.Reset();
        var httpClient = new HttpClient(new LambdaHandler(handler));
        RestHttpClientProvider.RegisterTestClient(configuration.Name, httpClient);

        var connection = new RestPublishConnection(configuration);
        return new RestPublishTransmitter(configuration, connection);
    }

    private sealed class LambdaHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public LambdaHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
