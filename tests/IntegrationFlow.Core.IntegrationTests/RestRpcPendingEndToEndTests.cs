using System.Text;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.RpcPending;
using IntegrationFlow.Contexts.Integrations._00Samples.RpcPending;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RestRpcPendingEndToEndTests : IAsyncLifetime, IDisposable
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
    public async Task RelayBatchAsync_CompletesPendingAfterCallbackWebhook()
    {
        server!.Given(Request.Create().WithPath("/v1/payments/authorize").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));

        var requestReplyConfiguration = CreateRequestReplyConfiguration(server.Url!);
        var webhookConfiguration = CreateWebhookConfiguration();
        var resolver = new FixedRestRpcPendingTransportResolver(requestReplyConfiguration, webhookConfiguration);
        var store = new InMemoryRpcPendingStore();
        var pendingId = Guid.NewGuid();
        var requestPayload = Encoding.UTF8.GetBytes("""{"amount":100}""");

        await store.EnqueueAsync(new RpcPendingRequest(
            pendingId,
            requestReplyConfiguration.Name,
            requestPayload,
            "application/json",
            DateTimeOffset.UtcNow));

        var relay = new RpcPendingRelayService(
            store,
            NullIntegrationLogger.Instance,
            new RpcPendingRelayOptions(),
            transportResolver: resolver);

        await relay.RelayBatchAsync(batchSize: 10);

        var requests = server.LogEntries;
        Assert.Single(requests);
        Assert.Equal(pendingId.ToString("N"), requests[0].RequestMessage.Headers!["Idempotency-Key"].First());
        Assert.Equal(
            "https://app.example.com/integrations/rpc-responses/payments",
            requests[0].RequestMessage.Headers!["X-Callback-Url"].First());

        var processor = new RestRpcResponseCorrelationProcessor();
        var message = new RestWebhookReceivedMessage(
            webhookConfiguration.Name,
            Encoding.UTF8.GetBytes("""{"status":"authorized"}"""),
            messageId: string.Empty,
            correlationId: pendingId.ToString("N"),
            contentType: "application/json",
            path: webhookConfiguration.Path,
            headers: null,
            receivedAt: DateTimeOffset.UtcNow);

        var correlationResult = await processor.ProcessAsync(
            message,
            requestReplyConfiguration,
            store,
            NullIntegrationLogger.Instance,
            metrics: null);

        Assert.Equal(RestRpcResponseProcessResult.Completed, correlationResult);

        var completed = await store.WaitForCompletionAsync(pendingId, TimeSpan.FromSeconds(5));
        Assert.NotNull(completed);
        Assert.Equal(RpcPendingStatus.Completed, completed!.Status);
        Assert.Equal("""{"status":"authorized"}""", Encoding.UTF8.GetString(completed.ResponsePayload!));
    }

    private static RestRequestReplyConfiguration CreateRequestReplyConfiguration(string baseAddress)
        => new()
        {
            Name = "PaymentAuth",
            BaseAddress = baseAddress,
            RequestPath = "/v1/payments/authorize",
            Method = "POST",
            ContentType = "application/json",
            ResponseTimeoutSeconds = 10,
            RequestMode = RestRequestReplyRequestMode.AsyncOutbox,
            ResponseWebhookProfileName = "PaymentRpcResponses",
            ResponseCallbackBaseUrl = "https://app.example.com",
            AcceptedStatusCodes = new[] { 200, 202, 204 },
            PendingTimeoutSeconds = 300,
        };

    private static RestWebhookConfiguration CreateWebhookConfiguration()
        => new()
        {
            Name = "PaymentRpcResponses",
            Path = "/integrations/rpc-responses/payments",
            CorrelationIdHeaderName = "X-Correlation-Id",
        };

    private sealed class FixedRestRpcPendingTransportResolver : IRpcPendingTransportResolver
    {
        private readonly RestRequestReplyConfiguration requestReplyConfiguration;
        private readonly RestWebhookConfiguration webhookConfiguration;

        public FixedRestRpcPendingTransportResolver(
            RestRequestReplyConfiguration requestReplyConfiguration,
            RestWebhookConfiguration webhookConfiguration)
        {
            this.requestReplyConfiguration = requestReplyConfiguration;
            this.webhookConfiguration = webhookConfiguration;
        }

        public IRpcPendingPublisher CreatePublisher(string profileName)
            => new RestRpcPendingPublisher(requestReplyConfiguration, webhookConfiguration);
    }
}
