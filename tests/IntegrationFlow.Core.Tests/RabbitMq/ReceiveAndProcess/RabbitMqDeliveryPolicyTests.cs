using System;
using System.Collections;
using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Listeners;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.ReceiveAndProcess;

public sealed class RabbitMqDeliveryPolicyTests
{
    [Fact]
    public void ShouldRequeue_ReturnsConfigurationValue_WhenMaxRetryNotReached()
    {
        var configuration = new RabbitMqConfiguration
        {
            RequeueOnFailure = true,
            MaxRetryCount = 0
        };

        Assert.True(RabbitMqDeliveryPolicy.ShouldRequeue(configuration, null));
    }

    [Fact]
    public void ShouldRequeue_ReturnsFalse_WhenDeathCountReached()
    {
        var configuration = new RabbitMqConfiguration
        {
            RequeueOnFailure = true,
            MaxRetryCount = 2
        };

        var headers = new Dictionary<string, object>
        {
            ["x-death"] = new ArrayList
            {
                new Dictionary<string, object> { ["count"] = 2L }
            }
        };

        Assert.False(RabbitMqDeliveryPolicy.ShouldRequeue(configuration, headers));
    }

    [Fact]
    public void GetDeathCount_ReturnsZero_WhenHeaderMissing()
    {
        Assert.Equal(0, RabbitMqDeliveryPolicy.GetDeathCount(null));
    }

    [Fact]
    public void GetDeathCount_ReadsCountFromHeader()
    {
        var headers = new Dictionary<string, object>
        {
            ["x-death"] = new ArrayList
            {
                new Dictionary<string, object> { ["count"] = 3L }
            }
        };

        Assert.Equal(3, RabbitMqDeliveryPolicy.GetDeathCount(headers));
    }
}
