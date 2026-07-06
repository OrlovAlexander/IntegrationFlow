using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Reply;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.SentAndWait;

public sealed class RabbitMqRequestReplyConfigurationMapperTests
{
    [Fact]
    public void ToListenerConfiguration_MapsRequestQueueAndConnection()
    {
        var source = new RabbitMqRequestReplyConfiguration
        {
            Name = "OrdersRpc",
            HostName = "rabbit.example.com",
            Port = 5671,
            UserName = "integration",
            Password = "secret",
            VirtualHost = "/prod",
            QueueName = "orders.rpc.requests",
            MaxConcurrentRequests = 4,
            SslEnabled = true,
            SslServerName = "rabbit.example.com",
            ClientProvidedName = "IntegrationFlow.OrdersRpcClient",
        };

        var listener = RabbitMqRequestReplyConfigurationMapper.ToListenerConfiguration(source);

        Assert.Equal("OrdersRpc", listener.Name);
        Assert.Equal("rabbit.example.com", listener.HostName);
        Assert.Equal(5671, listener.Port);
        Assert.Equal("integration", listener.UserName);
        Assert.Equal("secret", listener.Password);
        Assert.Equal("/prod", listener.VirtualHost);
        Assert.Equal("orders.rpc.requests", listener.QueueName);
        Assert.Equal((ushort)4, listener.PrefetchCount);
        Assert.True(listener.SslEnabled);
        Assert.Equal("rabbit.example.com", listener.SslServerName);
        Assert.Equal("IntegrationFlow.OrdersRpcServer", listener.ClientProvidedName);
    }
}

public sealed class RabbitMqRpcServerInboxMessageProcessingTests
{
    [Fact]
    public void ProcessInboxMessage_IgnoresNonRequestReplyMessage()
    {
        var configuration = CreateConfiguration();
        var processing = new RabbitMqRpcServerInboxMessageProcessing(configuration);

        var message = new RabbitMqReceivedMessage(
            new byte[] { 1 },
            deliveryTag: 1,
            routingKey: "rk",
            messageId: "msg",
            correlationId: "corr");

        processing.ProcessInboxMessage(new InboxMessage(message));
    }

    [Fact]
    public void ProcessInboxMessage_IgnoresNonRabbitMqMessage()
    {
        var configuration = CreateConfiguration();
        var processing = new RabbitMqRpcServerInboxMessageProcessing(configuration);

        processing.ProcessInboxMessage(new InboxMessage("not-rabbitmq"));
    }

    private static RabbitMqRequestReplyConfiguration CreateConfiguration()
        => new()
        {
            Name = "OrdersRpc",
            QueueName = "orders.rpc.requests",
            ResponseTimeoutSeconds = 30,
        };
}

public sealed class AddIntegrationFlowRabbitMqRpcServerTests
{
    [Fact]
    public void AddIntegrationFlowRabbitMqRpcServer_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationFlow();
        services.AddIntegrationFlowRabbitMqRpcServer("OrdersRpc");

        using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();

        Assert.NotEmpty(hostedServices);
    }
}
