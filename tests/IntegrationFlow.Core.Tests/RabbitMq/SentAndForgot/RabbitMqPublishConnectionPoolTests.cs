using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.SentAndForgot;

public sealed class RabbitMqPublishConnectionPoolTests
{
    [Fact]
    public void Invalidate_WhenPoolEmpty_DoesNotThrow()
    {
        var configuration = new RabbitMqPublishConfiguration
        {
            Name = "MissingProfile",
            HostName = "localhost",
            PublishTarget = RabbitMqPublishTarget.Queue,
            QueueName = "pool.test"
        };

        RabbitMqPublishConnectionPool.Invalidate(configuration);
    }

    [Fact]
    public void DisposeAll_DoesNotThrowWhenPoolsEmpty()
    {
        RabbitMqConnectionPoolRegistry.DisposeAll();
    }
}
