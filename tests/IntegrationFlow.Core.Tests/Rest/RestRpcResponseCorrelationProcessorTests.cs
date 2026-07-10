using System;
using System.Text;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00Samples.RpcPending;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Xunit;

namespace IntegrationFlow.Core.Tests.Rest;

public sealed class RestRpcResponseCorrelationProcessorTests
{
    [Fact]
    public async Task ProcessAsync_CompletesAwaitingPending_WhenCorrelationMatches()
    {
        var store = new InMemoryRpcPendingStore();
        var pendingId = Guid.NewGuid();
        await store.EnqueueAsync(new RpcPendingRequest(
            pendingId,
            "PaymentAuth",
            Encoding.UTF8.GetBytes("{}"),
            "application/json",
            DateTimeOffset.UtcNow));
        await store.ClaimPendingAsync(1, "worker-1", TimeSpan.FromMinutes(1));
        await store.MarkAwaitingResponseAsync(pendingId, "worker-1");

        var configuration = CreateConfiguration();
        var processor = new RestRpcResponseCorrelationProcessor();
        var message = new RestWebhookReceivedMessage(
            "PaymentRpcResponses",
            Encoding.UTF8.GetBytes("""{"status":"ok"}"""),
            messageId: string.Empty,
            correlationId: pendingId.ToString("N"),
            contentType: "application/json",
            path: configuration.Path,
            headers: null,
            receivedAt: DateTimeOffset.UtcNow);

        var result = await processor.ProcessAsync(
            message,
            CreateRequestReplyConfiguration(),
            store,
            NullIntegrationLogger.Instance,
            metrics: null);

        Assert.Equal(RestRpcResponseProcessResult.Completed, result);

        var pending = await store.GetByIdAsync(pendingId);
        Assert.NotNull(pending);
        Assert.Equal(RpcPendingStatus.Completed, pending!.Status);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsInvalidCorrelationId_WhenHeaderMissing()
    {
        var store = new InMemoryRpcPendingStore();
        var processor = new RestRpcResponseCorrelationProcessor();
        var message = new RestWebhookReceivedMessage(
            "PaymentRpcResponses",
            Array.Empty<byte>(),
            messageId: string.Empty,
            correlationId: string.Empty,
            contentType: "application/json",
            path: "/integrations/rpc-responses/payments",
            headers: null,
            receivedAt: DateTimeOffset.UtcNow);

        var result = await processor.ProcessAsync(
            message,
            CreateRequestReplyConfiguration(),
            store,
            NullIntegrationLogger.Instance,
            metrics: null);

        Assert.Equal(RestRpcResponseProcessResult.InvalidCorrelationId, result);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsDuplicateSkipped_WhenAlreadyCompleted()
    {
        var store = new InMemoryRpcPendingStore();
        var pendingId = Guid.NewGuid();
        await store.EnqueueAsync(new RpcPendingRequest(
            pendingId,
            "PaymentAuth",
            Encoding.UTF8.GetBytes("{}"),
            "application/json",
            DateTimeOffset.UtcNow));
        await store.CompleteAsync(pendingId, Encoding.UTF8.GetBytes("done"));

        var processor = new RestRpcResponseCorrelationProcessor();
        var message = new RestWebhookReceivedMessage(
            "PaymentRpcResponses",
            Encoding.UTF8.GetBytes("""{"status":"ok"}"""),
            messageId: string.Empty,
            correlationId: pendingId.ToString("N"),
            contentType: "application/json",
            path: "/integrations/rpc-responses/payments",
            headers: null,
            receivedAt: DateTimeOffset.UtcNow);

        var result = await processor.ProcessAsync(
            message,
            CreateRequestReplyConfiguration(),
            store,
            NullIntegrationLogger.Instance,
            metrics: null);

        Assert.Equal(RestRpcResponseProcessResult.DuplicateSkipped, result);
    }

    private static RestRequestReplyConfiguration CreateRequestReplyConfiguration()
        => new()
        {
            Name = "PaymentAuth",
            BaseAddress = "https://api.partner.example/",
            RequestPath = "/v1/payments/authorize",
            Method = "POST",
            RequestMode = RestRequestReplyRequestMode.AsyncOutbox,
            ResponseWebhookProfileName = "PaymentRpcResponses",
            ResponseCallbackBaseUrl = "https://app.example.com",
            PendingTimeoutSeconds = 300,
        };

    private static RestWebhookConfiguration CreateConfiguration()
        => new()
        {
            Name = "PaymentRpcResponses",
            Path = "/integrations/rpc-responses/payments",
        };
}
