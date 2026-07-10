using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndForgot.Transmitters;
using IntegrationFlow.Contexts.Integrations._00Samples.Outbox;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;
using Xunit;

namespace IntegrationFlow.Core.Tests.Outbox;

[Collection("RestHttp")]
public sealed class OutboxRelayServiceRestTests : IDisposable
{
    public void Dispose()
    {
        RestHttpClientProvider.Reset();
    }

    [Fact]
    public async Task RelayBatchAsync_PublishesRestOutboxMessage()
    {
        HttpRequestMessage? capturedRequest = null;
        var configuration = CreateConfiguration();
        var resolver = new TestRestOutboxTransportResolver(configuration, (request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain"),
            });
        });

        var store = new InMemoryOutboxStore();
        var outboxId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("{\"event\":\"created\"}");
        await store.EnqueueAsync(new OutboxMessage(
            outboxId,
            configuration.Name,
            payload,
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount: 0));

        var relay = new OutboxRelayService(
            store,
            NullIntegrationLogger.Instance,
            new OutboxRelayOptions { MaxAttempts = 3 },
            transportResolver: resolver);

        await relay.RelayBatchAsync(batchSize: 10);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal(outboxId.ToString("N"), capturedRequest.Headers.GetValues("Idempotency-Key").First());

        var pending = await store.GetPendingAsync(10);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task RelayBatchAsync_AbandonsRestMessage_On4xx()
    {
        var configuration = CreateConfiguration();
        configuration.RetryOnTransientErrors = false;
        var resolver = new TestRestOutboxTransportResolver(configuration, (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad", Encoding.UTF8, "text/plain"),
            }));

        var store = new InMemoryOutboxStore();
        var outboxId = Guid.NewGuid();
        await store.EnqueueAsync(new OutboxMessage(
            outboxId,
            configuration.Name,
            Encoding.UTF8.GetBytes("payload"),
            "application/json",
            DateTimeOffset.UtcNow,
            attemptCount: 0));

        var relay = new OutboxRelayService(
            store,
            NullIntegrationLogger.Instance,
            new OutboxRelayOptions { MaxAttempts = 3 },
            transportResolver: resolver);

        await relay.RelayBatchAsync(batchSize: 10);

        var pending = await store.GetPendingAsync(10);
        Assert.Empty(pending);
    }

    private static RestPublishConfiguration CreateConfiguration()
        => new()
        {
            Name = $"NotifyWebhook-{Guid.NewGuid():N}",
            BaseAddress = "https://api.example.com/",
            RequestPath = "/v1/events",
            Method = "POST",
            PublishTimeoutSeconds = 30,
            RetryOnTransientErrors = false,
        };

    private sealed class TestRestOutboxTransportResolver : IOutboxTransportResolver
    {
        private readonly RestPublishConfiguration configuration;
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public TestRestOutboxTransportResolver(
            RestPublishConfiguration configuration,
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            this.configuration = configuration;
            this.handler = handler;
        }

        public IOutboxRelayPublisher CreatePublisher(string profileName)
        {
            RestHttpClientProvider.Reset();
            var httpClient = new HttpClient(new LambdaHandler(handler));
            RestHttpClientProvider.RegisterTestClient(configuration.Name, httpClient);
            var connection = new RestPublishConnection(configuration);
            var transmitter = new RestPublishTransmitter(configuration, connection);
            return new TestOutboxRelayPublisher(OutboxTransportKind.Rest, transmitter, connection);
        }
    }

    private sealed class TestOutboxRelayPublisher : IOutboxRelayPublisher
    {
        private readonly IDisposable disposable;

        public TestOutboxRelayPublisher(
            OutboxTransportKind transportKind,
            ITransmitterWithResult transmitter,
            IDisposable disposable)
        {
            TransportKind = transportKind;
            Transmitter = transmitter;
            this.disposable = disposable;
        }

        public OutboxTransportKind TransportKind { get; }

        public ITransmitterWithResult Transmitter { get; }

        public void Dispose()
        {
            disposable.Dispose();
        }
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
