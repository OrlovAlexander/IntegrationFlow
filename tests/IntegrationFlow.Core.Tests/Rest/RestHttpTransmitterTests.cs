using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Exceptions;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Transmitters;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Cache;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using Xunit;

namespace IntegrationFlow.Core.Tests.Rest;

[Collection("RestHttp")]
public sealed class RestHttpTransmitterTests : IDisposable
{
    public void Dispose()
    {
        RestHttpClientProvider.Reset();
        RestClientResponseCacheRegistry.Reset();
        SentAndWaitIntegrationOptions.RetryOnTimeout = false;
        SentAndWaitIntegrationOptions.MaxRetries = 1;
    }

    [Fact]
    public async Task TransmitAsync_ReturnsBodyOnSuccess()
    {
        var configuration = CreateConfiguration();
        var transmitter = CreateTransmitter(configuration, (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json"),
            }));

        var result = await transmitter.TransmitAsync(
            new TransmitData("{\"id\":1}", "msg-1"),
            CancellationToken.None);

        Assert.False(result.IsFailed);
        Assert.Equal("{\"ok\":true}", result.Data);
    }

    [Fact]
    public async Task TransmitAsync_SetsIdempotencyHeaderFromMessageId()
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

        await transmitter.TransmitAsync(new TransmitData("payload", "stable-key"), CancellationToken.None);

        Assert.Equal("stable-key", idempotencyHeader);
    }

    [Fact]
    public async Task TransmitAsync_ReturnsFailedOn4xx()
    {
        var configuration = CreateConfiguration();
        var transmitter = CreateTransmitter(configuration, (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad request", Encoding.UTF8, "text/plain"),
            }));

        var result = await transmitter.TransmitAsync(new TransmitData("payload"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task TransmitAsync_ThrowsOn5xx_WhenRetriesDisabled()
    {
        var configuration = CreateConfiguration();
        configuration.RetryOnTransientErrors = false;
        var transmitter = CreateTransmitter(configuration, (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("error", Encoding.UTF8, "text/plain"),
            }));

        var exception = await Assert.ThrowsAsync<RestHttpException>(() =>
            transmitter.TransmitAsync(new TransmitData("payload"), CancellationToken.None));

        Assert.Equal(500, exception.StatusCode);
    }

    [Fact]
    public async Task TransmitAsync_ThrowsTimeoutException()
    {
        var configuration = CreateConfiguration(responseTimeoutSeconds: 1);
        configuration.RetryOnTransientErrors = false;
        var transmitter = CreateTransmitter(configuration, async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("late", Encoding.UTF8, "text/plain"),
            };
        });

        await Assert.ThrowsAsync<SentAndWaitTimeoutException>(() =>
            transmitter.TransmitAsync(new TransmitData("payload"), CancellationToken.None));
    }

    private static RestRequestReplyConfiguration CreateConfiguration(int responseTimeoutSeconds = 30)
        => new()
        {
            Name = $"TestProfile-{Guid.NewGuid():N}",
            BaseAddress = "https://api.example.com/",
            RequestPath = "/v1/test",
            Method = "POST",
            ResponseTimeoutSeconds = responseTimeoutSeconds,
        };

    private static RestHttpTransmitter CreateTransmitter(
        RestRequestReplyConfiguration configuration,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        RestHttpClientProvider.Reset();
        var httpClient = new HttpClient(new LambdaHandler(handler));
        RestHttpClientProvider.RegisterTestClient(configuration.Name, httpClient);

        var connection = new RestHttpConnection(configuration);
        return new RestHttpTransmitter(configuration, connection);
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
