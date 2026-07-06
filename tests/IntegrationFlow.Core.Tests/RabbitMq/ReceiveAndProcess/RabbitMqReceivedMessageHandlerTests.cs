using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.ReceiveAndProcess;

public sealed class RabbitMqReceivedMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_AcksAfterSuccessfulProcess()
    {
        var acknowledgement = new RecordingAcknowledgement();
        var handler = CreateHandler(acknowledgement, _ => Task.CompletedTask);

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 42),
            new RabbitMqConfiguration { RequeueOnFailure = false },
            CancellationToken.None);

        Assert.Equal(42UL, acknowledgement.AckedTag);
        Assert.Null(acknowledgement.NackedTag);
    }

    [Fact]
    public async Task HandleAsync_NacksWithRequeueOnProcessException()
    {
        var acknowledgement = new RecordingAcknowledgement();
        var handler = CreateHandler(
            acknowledgement,
            _ => Task.FromException(new InvalidOperationException("fail")));

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 7),
            new RabbitMqConfiguration { RequeueOnFailure = true },
            CancellationToken.None);

        Assert.Null(acknowledgement.AckedTag);
        Assert.Equal(7UL, acknowledgement.NackedTag);
        Assert.True(acknowledgement.NackRequeue);
    }

    [Fact]
    public async Task HandleAsync_NacksWithoutRequeueWhenConfigured()
    {
        var acknowledgement = new RecordingAcknowledgement();
        var handler = CreateHandler(
            acknowledgement,
            _ => Task.FromException(new InvalidOperationException("fail")));

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 8),
            new RabbitMqConfiguration { RequeueOnFailure = false },
            CancellationToken.None);

        Assert.False(acknowledgement.NackRequeue);
    }

    [Fact]
    public async Task HandleAsync_NacksRequeueOnInProgressDedup()
    {
        var acknowledgement = new RecordingAcknowledgement();
        var handler = CreateHandler(
            acknowledgement,
            _ => Task.FromException(new MessageProcessingInProgressException("msg-1")));

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 9),
            new RabbitMqConfiguration(),
            CancellationToken.None);

        Assert.Equal(9UL, acknowledgement.NackedTag);
        Assert.True(acknowledgement.NackRequeue);
    }

    [Fact]
    public async Task HandleAsync_NacksRequeueWhenCancellationRequestedBeforeProcess()
    {
        var acknowledgement = new RecordingAcknowledgement();
        var handler = CreateHandler(acknowledgement, _ => Task.CompletedTask);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 11),
            new RabbitMqConfiguration(),
            cts.Token);

        Assert.Null(acknowledgement.AckedTag);
        Assert.Equal(11UL, acknowledgement.NackedTag);
        Assert.True(acknowledgement.NackRequeue);
    }

    [Fact]
    public async Task HandleAsync_NacksRequeueWhenConsumerStoppingBeforeProcess()
    {
        var acknowledgement = new RecordingAcknowledgement();
        var handler = new RabbitMqReceivedMessageHandler(
            _ => Task.CompletedTask,
            acknowledgement,
            NullIntegrationLogger.Instance,
            isConsumerStopping: () => true);

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 12),
            new RabbitMqConfiguration(),
            CancellationToken.None);

        Assert.Null(acknowledgement.AckedTag);
        Assert.Equal(12UL, acknowledgement.NackedTag);
        Assert.True(acknowledgement.NackRequeue);
    }

    [Fact]
    public async Task HandleAsync_NacksRequeueWhenCancellationRequestedAfterProcess()
    {
        var acknowledgement = new RecordingAcknowledgement();
        using var cts = new CancellationTokenSource();
        var handler = new RabbitMqReceivedMessageHandler(
            _ =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            },
            acknowledgement,
            NullIntegrationLogger.Instance);

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 13),
            new RabbitMqConfiguration(),
            cts.Token);

        Assert.Null(acknowledgement.AckedTag);
        Assert.Equal(13UL, acknowledgement.NackedTag);
        Assert.True(acknowledgement.NackRequeue);
    }

    [Fact]
    public async Task HandleAsync_NacksWithRequeueOnProcessException_RecordsRequeueMetric()
    {
        var metrics = new RecordingConsumerMetrics();
        var acknowledgement = new RecordingAcknowledgement();
        var handler = CreateHandler(
            acknowledgement,
            _ => Task.FromException(new InvalidOperationException("fail")),
            metrics,
            "Inbox");

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 7),
            new RabbitMqConfiguration { RequeueOnFailure = true },
            CancellationToken.None);

        Assert.Equal(1, metrics.OutcomeCount);
        Assert.Equal(ConsumerOutcomeReason.Requeue, metrics.LastReason);
        Assert.Equal("Inbox", metrics.LastProfile);
    }

    [Fact]
    public async Task HandleAsync_NacksWithoutRequeueWhenConfigured_RecordsNackMetric()
    {
        var metrics = new RecordingConsumerMetrics();
        var acknowledgement = new RecordingAcknowledgement();
        var handler = CreateHandler(
            acknowledgement,
            _ => Task.FromException(new InvalidOperationException("fail")),
            metrics,
            "Inbox");

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 8),
            new RabbitMqConfiguration { RequeueOnFailure = false },
            CancellationToken.None);

        Assert.Equal(ConsumerOutcomeReason.Nack, metrics.LastReason);
    }

    [Fact]
    public async Task HandleAsync_NacksRequeueOnInProgressDedup_RecordsInProgressRequeueMetric()
    {
        var metrics = new RecordingConsumerMetrics();
        var acknowledgement = new RecordingAcknowledgement();
        var handler = CreateHandler(
            acknowledgement,
            _ => Task.FromException(new MessageProcessingInProgressException("msg-1")),
            metrics,
            "Inbox");

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 9),
            new RabbitMqConfiguration(),
            CancellationToken.None);

        Assert.Equal(ConsumerOutcomeReason.InProgressRequeue, metrics.LastReason);
    }

    private static RabbitMqReceivedMessageHandler CreateHandler(
        IRabbitMqMessageAcknowledgement acknowledgement,
        Func<object, Task> process,
        IIntegrationFlowMetrics? metrics = null,
        string? profileName = null)
        => new(process, acknowledgement, NullIntegrationLogger.Instance, metrics: metrics, profileName: profileName);

    [Fact]
    public async Task HandleAsync_NacksWithoutRequeueWhenDeathCountReached()
    {
        var acknowledgement = new RecordingAcknowledgement();
        var handler = CreateHandler(
            acknowledgement,
            _ => Task.FromException(new InvalidOperationException("fail")));

        var headers = new Dictionary<string, object>
        {
            ["x-death"] = new ArrayList
            {
                new Dictionary<string, object> { ["count"] = 2L }
            }
        };

        await handler.HandleAsync(
            CreateMessage(deliveryTag: 10, headers),
            new RabbitMqConfiguration { RequeueOnFailure = true, MaxRetryCount = 2 },
            CancellationToken.None);

        Assert.False(acknowledgement.NackRequeue);
    }

    private static RabbitMqReceivedMessage CreateMessage(ulong deliveryTag, IDictionary<string, object>? headers = null)
        => new(new byte[] { 1 }, deliveryTag, "rk", "msg-id", "corr-id", headers: headers);

    private sealed class RecordingConsumerMetrics : IIntegrationFlowMetrics
    {
        public int OutcomeCount { get; private set; }

        public string? LastProfile { get; private set; }

        public string? LastReason { get; private set; }

        public void RecordMessageProcessed(string profileName, TimeSpan duration, bool success)
        {
        }

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

        public void RecordRequestReply(string profileName, TimeSpan duration, bool success, bool timedOut = false)
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
        {
            OutcomeCount++;
            LastProfile = profileName;
            LastReason = reason;
        }

        public void RecordConnectionPoolSize(string kind, int size)
        {
        }

        public void RecordBrokerConnected(string profileName, string kind, bool connected)
        {
        }
    }

    private sealed class RecordingAcknowledgement : IRabbitMqMessageAcknowledgement
    {
        public ulong? AckedTag { get; private set; }

        public ulong? NackedTag { get; private set; }

        public bool NackRequeue { get; private set; }

        public void Acknowledge(ulong deliveryTag) => AckedTag = deliveryTag;

        public void NegativeAcknowledge(ulong deliveryTag, bool requeue)
        {
            NackedTag = deliveryTag;
            NackRequeue = requeue;
        }
    }
}
