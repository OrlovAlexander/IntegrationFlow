using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.SentAndWait;

public sealed class RabbitMqRequestReplyConfigurationLoaderTests
{
    [Fact]
    public void LoadFromFile_ReadsLegacyFlatSettings()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqRequestReply": {
                "HostName": "rabbit.example.com",
                "Port": 5673,
                "UserName": "integration",
                "Password": "secret",
                "VirtualHost": "/prod",
                "RequestTarget": "Queue",
                "QueueName": "orders.rpc",
                "ReplyMode": "ExclusiveQueue",
                "ResponseTimeoutSeconds": 15,
                "ContentType": "text/plain",
                "Persistent": false,
                "Mandatory": true,
                "ValidateTopology": false,
                "AutomaticRecoveryEnabled": false,
                "ClientProvidedName": "IntegrationFlow.TestRpcClient"
              }
            }
            """);

        var configuration = RabbitMqRequestReplyConfigurationLoader.LoadFromFile(configPath);

        Assert.Equal(RabbitMqRequestReplyConfigurationLoader.DefaultProfileName, configuration.Name);
        Assert.Equal("rabbit.example.com", configuration.HostName);
        Assert.Equal(5673, configuration.Port);
        Assert.Equal("integration", configuration.UserName);
        Assert.Equal("secret", configuration.Password);
        Assert.Equal("/prod", configuration.VirtualHost);
        Assert.Equal(RabbitMqRequestReplyTarget.Queue, configuration.RequestTarget);
        Assert.Equal("orders.rpc", configuration.QueueName);
        Assert.Equal(RabbitMqReplyMode.ExclusiveQueue, configuration.ReplyMode);
        Assert.Equal(15, configuration.ResponseTimeoutSeconds);
        Assert.Equal("text/plain", configuration.ContentType);
        Assert.False(configuration.Persistent);
        Assert.True(configuration.Mandatory);
        Assert.False(configuration.ValidateTopology);
        Assert.False(configuration.AutomaticRecoveryEnabled);
        Assert.Equal("IntegrationFlow.TestRpcClient", configuration.ClientProvidedName);
    }

    [Fact]
    public void LoadAll_ReadsNamedProfiles()
    {
        var configPath = CreateNamedConfigFile();

        var profiles = RabbitMqRequestReplyConfigurationLoader.LoadAll(configPath);

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, profile => profile.Name == "OrdersRpc" && profile.QueueName == "orders.rpc.requests");
        Assert.Contains(profiles, profile => profile.Name == "InventoryRpc" && profile.Exchange == "integration.rpc");
    }

    [Fact]
    public void LoadProfile_ReadsNamedProfileByName()
    {
        var configPath = CreateNamedConfigFile();

        var configuration = RabbitMqRequestReplyConfigurationLoader.LoadProfile("InventoryRpc", configPath);

        Assert.Equal("InventoryRpc", configuration.Name);
        Assert.Equal(RabbitMqRequestReplyTarget.Exchange, configuration.RequestTarget);
        Assert.Equal("integration.rpc", configuration.Exchange);
        Assert.Equal("inventory.lookup", configuration.RoutingKey);
        Assert.Equal(RabbitMqReplyMode.DirectReplyTo, configuration.ReplyMode);
    }

    [Fact]
    public void LoadFromFile_ReadsMaxConcurrentRequestsAndReuseConnection()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqRequestReply": {
                "HostName": "localhost",
                "RequestTarget": "Queue",
                "QueueName": "orders.rpc",
                "MaxConcurrentRequests": 8,
                "ReuseConnection": true
              }
            }
            """);

        var configuration = RabbitMqRequestReplyConfigurationLoader.LoadFromFile(configPath);

        Assert.Equal(8, configuration.MaxConcurrentRequests);
        Assert.True(configuration.ReuseConnection);
    }

    [Fact]
    public void LoadFromFile_ReadsReuseReplyConnectionAndSslSettings()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqRequestReply": {
                "HostName": "rabbit.example.com",
                "Port": 5671,
                "RequestTarget": "Queue",
                "QueueName": "orders.rpc",
                "ReuseReplyConnection": false,
                "SslEnabled": true,
                "SslServerName": "rabbit.example.com"
              }
            }
            """);

        var configuration = RabbitMqRequestReplyConfigurationLoader.LoadFromFile(configPath);

        Assert.False(configuration.ReuseReplyConnection);
        Assert.True(configuration.SslEnabled);
        Assert.Equal("rabbit.example.com", configuration.SslServerName);
    }

    [Fact]
    public void Validate_ThrowsWhenQueueTargetWithoutQueueName()
    {
        var configuration = new RabbitMqRequestReplyConfiguration
        {
            RequestTarget = RabbitMqRequestReplyTarget.Queue
        };

        Assert.Throws<InvalidOperationException>(() => configuration.Validate());
    }

    [Fact]
    public void GetRequestRouting_ResolvesQueueAndExchangeTargets()
    {
        var queueConfiguration = new RabbitMqRequestReplyConfiguration
        {
            RequestTarget = RabbitMqRequestReplyTarget.Queue,
            QueueName = "orders.rpc.requests"
        };
        queueConfiguration.Validate();

        Assert.Equal(string.Empty, queueConfiguration.GetRequestExchange());
        Assert.Equal("orders.rpc.requests", queueConfiguration.GetRequestRoutingKey());

        var exchangeConfiguration = new RabbitMqRequestReplyConfiguration
        {
            RequestTarget = RabbitMqRequestReplyTarget.Exchange,
            Exchange = "integration.rpc",
            RoutingKey = "inventory.lookup"
        };
        exchangeConfiguration.Validate();

        Assert.Equal("integration.rpc", exchangeConfiguration.GetRequestExchange());
        Assert.Equal("inventory.lookup", exchangeConfiguration.GetRequestRoutingKey());
    }

    [Fact]
    public void RabbitMqReceivedMessage_IsRequestReply_WhenReplyToPresent()
    {
        var message = new RabbitMqReceivedMessage(
            Array.Empty<byte>(),
            1,
            "orders.rpc.requests",
            "msg-1",
            "corr-1",
            "amq.rabbitmq.reply-to");

        Assert.True(message.IsRequestReply);
        Assert.Equal("amq.rabbitmq.reply-to", message.ReplyTo);
    }

    private static string CreateNamedConfigFile()
    {
        return CreateConfigFile(
            """
            {
              "RabbitMqRequestReply": {
                "OrdersRpc": {
                  "HostName": "localhost",
                  "RequestTarget": "Queue",
                  "QueueName": "orders.rpc.requests",
                  "ReplyMode": "DirectReplyTo",
                  "ResponseTimeoutSeconds": 30
                },
                "InventoryRpc": {
                  "HostName": "localhost",
                  "RequestTarget": "Exchange",
                  "Exchange": "integration.rpc",
                  "RoutingKey": "inventory.lookup",
                  "ReplyMode": "DirectReplyTo",
                  "ResponseTimeoutSeconds": 10
                }
              }
            }
            """);
    }

    private static string CreateConfigFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(filePath, content);
        return filePath;
    }
}
