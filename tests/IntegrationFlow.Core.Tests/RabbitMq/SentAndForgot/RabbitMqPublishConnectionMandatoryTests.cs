using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Exceptions;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.SentAndForgot;

public sealed class RabbitMqPublishConnectionMandatoryTests
{
    [Fact]
    public void EnsureNotUnroutable_AfterSimulatedBasicReturn_Throws()
    {
        using var connection = CreateConnection(mandatory: true);
        connection.SimulateBasicReturnForTesting();
        connection.WaitForUnroutableProcessing(TimeSpan.FromSeconds(1));

        Assert.Throws<UnroutableMessageException>(() => connection.EnsureNotUnroutable());
    }

    [Fact]
    public void EnsureNotUnroutable_WithoutBasicReturn_DoesNotThrow()
    {
        using var connection = CreateConnection(mandatory: true);
        connection.WaitForUnroutableProcessing(TimeSpan.FromMilliseconds(100));

        connection.EnsureNotUnroutable();
    }

    [Fact]
    public void WaitForUnroutableProcessing_WhenNotMandatory_IsNoOp()
    {
        using var connection = CreateConnection(mandatory: false);
        connection.SimulateBasicReturnForTesting();
        connection.WaitForUnroutableProcessing(TimeSpan.FromMilliseconds(100));

        connection.EnsureNotUnroutable();
    }

    private static RabbitMqPublishConnection CreateConnection(bool mandatory)
    {
        var configuration = new RabbitMqPublishConfiguration
        {
            HostName = "localhost",
            QueueName = "mandatory-test",
            PublishTarget = RabbitMqPublishTarget.Queue,
            Mandatory = mandatory,
            PublisherConfirmsEnabled = false
        };

        return new RabbitMqPublishConnection(configuration, openConnection: false);
    }
}
