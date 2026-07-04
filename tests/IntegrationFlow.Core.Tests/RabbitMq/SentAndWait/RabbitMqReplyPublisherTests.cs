using System.Text;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.SentAndWait;

public sealed class RabbitMqReplyPublisherTests
{
    [Fact]
    public void PublishReply_ThrowsWhenRequestHasNoReplyTo()
    {
        var publisher = new RabbitMqReplyPublisher(new RabbitMqRequestReplyConfiguration
        {
            RequestTarget = RabbitMqRequestReplyTarget.Queue,
            QueueName = "test.rpc"
        });
        var request = new RabbitMqReceivedMessage(
            Encoding.UTF8.GetBytes("request"),
            1,
            "q",
            "m1",
            "c1");

        Assert.Throws<InvalidOperationException>(() => publisher.PublishReply(request, "response"));
    }

    [Fact]
    public void PublishReply_ThrowsWhenReplyToMissing()
    {
        var publisher = new RabbitMqReplyPublisher(new RabbitMqRequestReplyConfiguration
        {
            RequestTarget = RabbitMqRequestReplyTarget.Queue,
            QueueName = "test.rpc"
        });

        Assert.Throws<ArgumentException>(() => publisher.PublishReply(string.Empty, "c1", "response"));
    }
}
