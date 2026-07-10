using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Auth;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00Samples.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace IntegrationFlow.Core.Tests.Rest;

public sealed class RestWebhookMessageProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ReturnsSuccessWhenHandlerCompletes()
    {
        var processor = CreateProcessor();
        var context = CreateHttpContext("{\"event\":\"created\"}", "msg-1");
        var configuration = CreateConfiguration();

        var result = await processor.ProcessAsync(
            context,
            configuration,
            (_, _) => Task.CompletedTask,
            NullIntegrationLogger.Instance,
            metrics: null,
            deduplicationStore: null,
            authenticator: null,
            CancellationToken.None);

        Assert.Equal(RestWebhookProcessResult.Success, result);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsDuplicateSkippedWhenAlreadyProcessed()
    {
        var store = new InMemoryMessageDeduplicationStore();
        await store.TryBeginProcessingAsync("dup-1");
        await store.MarkProcessedAsync("dup-1");

        var processor = CreateProcessor();
        var context = CreateHttpContext("payload", "dup-1");
        var configuration = CreateConfiguration();
        var metrics = new TestMetrics();

        var result = await processor.ProcessAsync(
            context,
            configuration,
            (_, _) => Task.FromException(new InvalidOperationException("should not run")),
            NullIntegrationLogger.Instance,
            metrics,
            store,
            authenticator: null,
            CancellationToken.None);

        Assert.Equal(RestWebhookProcessResult.DuplicateSkipped, result);
        Assert.Equal(1, metrics.ConsumerOutcomeCount);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsInProgressWhenLockHeld()
    {
        var store = new InMemoryMessageDeduplicationStore();
        await store.TryBeginProcessingAsync("in-flight");

        var processor = CreateProcessor();
        var context = CreateHttpContext("payload", "in-flight");
        var configuration = CreateConfiguration();

        var result = await processor.ProcessAsync(
            context,
            configuration,
            (_, _) => Task.CompletedTask,
            NullIntegrationLogger.Instance,
            metrics: null,
            store,
            authenticator: null,
            CancellationToken.None);

        Assert.Equal(RestWebhookProcessResult.InProgress, result);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsHandlerFailedWhenHandlerThrows()
    {
        var processor = CreateProcessor();
        var context = CreateHttpContext("payload", "fail-1");
        var configuration = CreateConfiguration();
        var metrics = new TestMetrics();

        var result = await processor.ProcessAsync(
            context,
            configuration,
            (_, _) => Task.FromException(new InvalidOperationException("boom")),
            NullIntegrationLogger.Instance,
            metrics,
            deduplicationStore: null,
            authenticator: null,
            CancellationToken.None);

        Assert.Equal(RestWebhookProcessResult.HandlerFailed, result);
        Assert.False(metrics.LastSuccess);
        Assert.Equal(1, metrics.ConsumerOutcomeCount);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsPayloadTooLargeWhenBodyExceedsLimit()
    {
        var processor = CreateProcessor();
        var context = CreateHttpContext(new string('x', 32), messageId: "large-1");
        var configuration = CreateConfiguration(maxBodyBytes: 16);

        var result = await processor.ProcessAsync(
            context,
            configuration,
            (_, _) => Task.CompletedTask,
            NullIntegrationLogger.Instance,
            metrics: null,
            deduplicationStore: null,
            authenticator: null,
            CancellationToken.None);

        Assert.Equal(RestWebhookProcessResult.PayloadTooLarge, result);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsMissingMessageIdWhenRequired()
    {
        var processor = CreateProcessor();
        var context = CreateHttpContext("payload", messageId: null);
        var configuration = CreateConfiguration(requireMessageId: true);

        var result = await processor.ProcessAsync(
            context,
            configuration,
            (_, _) => Task.CompletedTask,
            NullIntegrationLogger.Instance,
            metrics: null,
            deduplicationStore: null,
            authenticator: null,
            CancellationToken.None);

        Assert.Equal(RestWebhookProcessResult.MissingMessageId, result);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsUnauthorizedWhenAuthenticatorRejects()
    {
        var processor = CreateProcessor();
        var context = CreateHttpContext("payload", "auth-1");
        var configuration = CreateConfiguration();

        var result = await processor.ProcessAsync(
            context,
            configuration,
            (_, _) => Task.CompletedTask,
            NullIntegrationLogger.Instance,
            metrics: null,
            deduplicationStore: null,
            new RejectingAuthenticator(),
            CancellationToken.None);

        Assert.Equal(RestWebhookProcessResult.Unauthorized, result);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsMethodNotAllowedForGet()
    {
        var processor = CreateProcessor();
        var context = CreateHttpContext("payload", "get-1");
        context.Request.Method = HttpMethods.Get;
        var configuration = CreateConfiguration();

        var result = await processor.ProcessAsync(
            context,
            configuration,
            (_, _) => Task.CompletedTask,
            NullIntegrationLogger.Instance,
            metrics: null,
            deduplicationStore: null,
            authenticator: null,
            CancellationToken.None);

        Assert.Equal(RestWebhookProcessResult.MethodNotAllowed, result);
    }

    [Theory]
    [InlineData(RestWebhookProcessResult.Success, HttpStatusCode.OK)]
    [InlineData(RestWebhookProcessResult.DuplicateSkipped, HttpStatusCode.OK)]
    [InlineData(RestWebhookProcessResult.InProgress, HttpStatusCode.ServiceUnavailable)]
    [InlineData(RestWebhookProcessResult.HandlerFailed, HttpStatusCode.InternalServerError)]
    [InlineData(RestWebhookProcessResult.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData(RestWebhookProcessResult.PayloadTooLarge, HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(RestWebhookProcessResult.MissingMessageId, HttpStatusCode.BadRequest)]
    [InlineData(RestWebhookProcessResult.MethodNotAllowed, HttpStatusCode.MethodNotAllowed)]
    public void ToStatusCode_MapsKnownResults(RestWebhookProcessResult result, HttpStatusCode expected)
    {
        Assert.Equal((int)expected, RestWebhookProcessResultMapper.ToStatusCode(result));
    }

    private static RestWebhookMessageProcessor CreateProcessor()
        => new();

    private static RestWebhookConfiguration CreateConfiguration(
        int maxBodyBytes = 1024,
        bool requireMessageId = false)
        => new()
        {
            Name = "OrdersInbox",
            Path = "/integrations/webhooks/orders",
            MessageIdHeaderName = "X-Webhook-Id",
            MaxBodyBytes = maxBodyBytes,
            RequireMessageId = requireMessageId,
            AllowedMethods = new[] { "POST" },
        };

    private static DefaultHttpContext CreateHttpContext(string body, string? messageId)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/integrations/webhooks/orders";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        if (!string.IsNullOrWhiteSpace(messageId))
        {
            context.Request.Headers["X-Webhook-Id"] = messageId;
        }

        return context;
    }

    private sealed class RejectingAuthenticator : IRestWebhookAuthenticator
    {
        public Task<bool> TryAuthenticateAsync(
            HttpContext httpContext,
            RestWebhookConfiguration configuration,
            RestWebhookReceivedMessage message,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class TestMetrics : IIntegrationFlowMetrics
    {
        public bool LastSuccess { get; private set; }

        public int ConsumerOutcomeCount { get; private set; }

        public void RecordMessageProcessed(string profileName, TimeSpan duration, bool success)
            => LastSuccess = success;

        public void RecordOutboxRelayPublished(int count)
        {
        }

        public void RecordOutboxRelayFailed(int count)
        {
        }

        public void RecordOutboxRelayAbandoned(int count)
        {
        }

        public void RecordOutboxPending(int count)
        {
        }

        public void RecordRequestReply(
            string profileName,
            TimeSpan duration,
            bool success,
            bool timedOut = false,
            string? transport = null)
        {
        }

        public void RecordRequestReplyRetryAfterTimeout(string profileName)
        {
        }

        public void RecordRpcPendingRelayPublished(int count)
        {
        }

        public void RecordRpcPendingRelayFailed(int count)
        {
        }

        public void RecordRpcPendingRelayAbandoned(int count)
        {
        }

        public void RecordRpcPendingAwaiting(int count)
        {
        }

        public void RecordRpcPendingCompleted(string profileName, TimeSpan duration, bool success, bool timedOut = false)
        {
        }

        public void RecordListenerReconnect(string profileName)
        {
        }

        public void RecordListenerShutdownRequeue(string profileName)
        {
        }

        public void RecordConsumerOutcome(string profileName, string reason)
            => ConsumerOutcomeCount++;

        public void RecordConnectionPoolSize(string kind, int size)
        {
        }

        public void RecordBrokerConnected(string profileName, string kind, bool connected)
        {
        }
    }
}
