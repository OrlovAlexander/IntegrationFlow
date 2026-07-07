using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._03Domain;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq;

public sealed class RabbitMqTopologyHelperTests
{
    [Fact]
    public void EnsureQueue_DeclareTopologyOnStartup_UsesActiveDeclare()
    {
        var channel = new RecordingTopologyChannel();

        RabbitMqTopologyHelper.EnsureQueue(
            channel,
            "dev.inbox",
            new RabbitMqTopologyHelper.TopologyOptions
            {
                ValidateTopology = true,
                DeclareTopologyOnStartup = true,
            },
            new RecordingLogger(),
            "Inbox");

        Assert.Equal(1, channel.ActiveQueueDeclareCount);
        Assert.Equal(0, channel.PassiveQueueDeclareCount);
    }

    [Fact]
    public void EnsureQueue_DefaultOptions_UsesPassiveDeclare()
    {
        var channel = new RecordingTopologyChannel();

        RabbitMqTopologyHelper.EnsureQueue(
            channel,
            "prod.inbox",
            new RabbitMqTopologyHelper.TopologyOptions
            {
                ValidateTopology = true,
                DeclareTopologyOnStartup = false,
            });

        Assert.Equal(0, channel.ActiveQueueDeclareCount);
        Assert.Equal(1, channel.PassiveQueueDeclareCount);
        Assert.Equal("prod.inbox", channel.LastPassiveQueue);
    }

    [Fact]
    public void EnsureQueue_ValidateTopologyDisabled_SkipsDeclare()
    {
        var channel = new RecordingTopologyChannel();

        RabbitMqTopologyHelper.EnsureQueue(
            channel,
            "skipped",
            new RabbitMqTopologyHelper.TopologyOptions
            {
                ValidateTopology = false,
                DeclareTopologyOnStartup = true,
            });

        Assert.Equal(0, channel.ActiveQueueDeclareCount);
        Assert.Equal(0, channel.PassiveQueueDeclareCount);
    }

    [Fact]
    public void EnsureExchange_DeclareTopologyOnStartup_UsesActiveDeclareWithType()
    {
        var channel = new RecordingTopologyChannel();

        RabbitMqTopologyHelper.EnsureExchange(
            channel,
            "integration.events",
            new RabbitMqTopologyHelper.TopologyOptions
            {
                ValidateTopology = true,
                DeclareTopologyOnStartup = true,
                ExchangeType = ExchangeType.Topic,
            });

        Assert.Equal(1, channel.ActiveExchangeDeclareCount);
        Assert.Equal(ExchangeType.Topic, channel.LastExchangeType);
    }

    private sealed class RecordingLogger : IIntegrationLogger
    {
        public void LogException(string message, Exception ex)
        {
        }

        public void LogWarn(string message)
        {
        }

        public void Log(string message)
        {
        }

        public void Log(string format, params object[] args)
        {
        }

        public void LogInfo(string message)
        {
        }
    }

    private sealed class RecordingTopologyChannel : RabbitMqTopologyHelper.IRabbitMqTopologyChannel
    {
        public int PassiveQueueDeclareCount { get; private set; }

        public int ActiveQueueDeclareCount { get; private set; }

        public int PassiveExchangeDeclareCount { get; private set; }

        public int ActiveExchangeDeclareCount { get; private set; }

        public string? LastPassiveQueue { get; private set; }

        public string? LastExchangeType { get; private set; }

        public void ExchangeDeclare(
            string exchange,
            string type,
            bool durable,
            bool autoDelete,
            IDictionary<string, object>? arguments)
        {
            ActiveExchangeDeclareCount++;
            LastExchangeType = type;
        }

        public void ExchangeDeclarePassive(string exchange)
        {
            PassiveExchangeDeclareCount++;
        }

        public void QueueDeclare(
            string queue,
            bool durable,
            bool exclusive,
            bool autoDelete,
            IDictionary<string, object>? arguments)
        {
            ActiveQueueDeclareCount++;
        }

        public void QueueDeclarePassive(string queue)
        {
            PassiveQueueDeclareCount++;
            LastPassiveQueue = queue;
        }
    }
}
