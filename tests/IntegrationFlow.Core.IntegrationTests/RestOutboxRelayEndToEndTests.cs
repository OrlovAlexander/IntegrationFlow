using System;
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
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RestOutboxRelayEndToEndTests : IAsyncLifetime, IDisposable
{
    private WireMockServer? server;

    public Task InitializeAsync()
    {
        server = WireMockServer.Start();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        server?.Dispose();
        RestHttpClientProvider.Reset();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        server?.Dispose();
        RestHttpClientProvider.Reset();
    }

    [Fact]
    public async Task RelayBatchAsync_DeliversOutboxMessageToWebhook()
    {
        server!.Given(Request.Create().WithPath("/v1/events").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var configuration = CreateConfiguration(server.Url!);
        var resolver = new RestOutboxRelayTestResolver(configuration);
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
            new OutboxRelayOptions(),
            transportResolver: resolver);

        await relay.RelayBatchAsync(batchSize: 10);

        var requests = server.LogEntries;
        Assert.Single(requests);
        Assert.Equal(outboxId.ToString("N"), requests[0].RequestMessage.Headers!["Idempotency-Key"].First());
        Assert.Equal("{\"event\":\"created\"}", requests[0].RequestMessage.Body);
    }

    private static RestPublishConfiguration CreateConfiguration(string baseAddress)
        => new()
        {
            Name = "NotifyWebhook",
            BaseAddress = baseAddress.EndsWith('/') ? baseAddress : baseAddress + "/",
            RequestPath = "/v1/events",
            Method = "POST",
            PublishTimeoutSeconds = 10,
            ExpectedStatusCodes = new[] { 200, 202, 204 },
            RetryOnTransientErrors = false,
        };

    private sealed class RestOutboxRelayTestResolver : IOutboxTransportResolver
    {
        private readonly RestPublishConfiguration configuration;

        public RestOutboxRelayTestResolver(RestPublishConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public IOutboxRelayPublisher CreatePublisher(string profileName)
        {
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
}
