using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
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
            null,
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
            null,
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
            null,
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
            null,
            CancellationToken.None);

        Assert.Equal(9UL, acknowledgement.NackedTag);
        Assert.True(acknowledgement.NackRequeue);
    }

    private static RabbitMqReceivedMessageHandler CreateHandler(
        IRabbitMqMessageAcknowledgement acknowledgement,
        Func<object, Task> process)
        => new(process, acknowledgement, NullIntegrationLogger.Instance);

    private static RabbitMqReceivedMessage CreateMessage(ulong deliveryTag)
        => new(new byte[] { 1 }, deliveryTag, "rk", "msg-id", "corr-id");

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
