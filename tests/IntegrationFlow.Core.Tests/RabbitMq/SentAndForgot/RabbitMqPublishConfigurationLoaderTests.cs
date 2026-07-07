using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.SentAndForgot;

public sealed class RabbitMqPublishConfigurationLoaderTests
{
    [Fact]
    public void LoadFromFile_ReadsLegacyFlatSettings()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqPublish": {
                "HostName": "rabbit.example.com",
                "Port": 5673,
                "UserName": "integration",
                "Password": "secret",
                "VirtualHost": "/prod",
                "PublishTarget": "Queue",
                "QueueName": "orders.outbox",
                "ContentType": "text/plain",
                "Persistent": false,
                "Priority": 5,
                "ExpirationMilliseconds": 120000,
                "Mandatory": true,
                "ValidateTopology": false,
                "AutomaticRecoveryEnabled": false,
                "ClientProvidedName": "IntegrationFlow.TestPublisher"
              }
            }
            """);

        var configuration = RabbitMqPublishConfigurationLoader.LoadFromFile(configPath);

        Assert.Equal(RabbitMqPublishConfigurationLoader.DefaultProfileName, configuration.Name);
        Assert.Equal("rabbit.example.com", configuration.HostName);
        Assert.Equal(5673, configuration.Port);
        Assert.Equal("integration", configuration.UserName);
        Assert.Equal("secret", configuration.Password);
        Assert.Equal("/prod", configuration.VirtualHost);
        Assert.Equal(RabbitMqPublishTarget.Queue, configuration.PublishTarget);
        Assert.Equal("orders.outbox", configuration.QueueName);
        Assert.Equal("text/plain", configuration.ContentType);
        Assert.False(configuration.Persistent);
        Assert.Equal((byte)5, configuration.Priority);
        Assert.Equal(120_000, configuration.ExpirationMilliseconds);
        Assert.True(configuration.Mandatory);
        Assert.False(configuration.ValidateTopology);
        Assert.False(configuration.AutomaticRecoveryEnabled);
        Assert.Equal("IntegrationFlow.TestPublisher", configuration.ClientProvidedName);
    }

    [Fact]
    public void LoadAll_ReadsNamedProfiles()
    {
        var configPath = CreateNamedConfigFile();

        var profiles = RabbitMqPublishConfigurationLoader.LoadAll(configPath);

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, profile => profile.Name == "OrdersOut" && profile.QueueName == "orders.outbox");
        Assert.Contains(profiles, profile => profile.Name == "EventsOut" && profile.Exchange == "integration.events");
    }

    [Fact]
    public void LoadProfile_ReadsNamedProfileByName()
    {
        var configPath = CreateNamedConfigFile();

        var configuration = RabbitMqPublishConfigurationLoader.LoadProfile("EventsOut", configPath);

        Assert.Equal("EventsOut", configuration.Name);
        Assert.Equal(RabbitMqPublishTarget.Exchange, configuration.PublishTarget);
        Assert.Equal("integration.events", configuration.Exchange);
        Assert.Equal("order.created", configuration.RoutingKey);
        Assert.Equal("IntegrationFlow.EventsPublisher", configuration.ClientProvidedName);
    }

    [Fact]
    public void LoadSingle_ReturnsSingleProfileFromNamedFileWhenOnlyOneProfileExists()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqPublish": {
                "OrdersOut": {
                  "HostName": "localhost",
                  "PublishTarget": "Queue",
                  "QueueName": "orders.outbox"
                }
              }
            }
            """);

        var configuration = RabbitMqPublishConfigurationLoader.LoadSingle(configPath);

        Assert.Equal("OrdersOut", configuration.Name);
        Assert.Equal("orders.outbox", configuration.QueueName);
    }

    [Fact]
    public void LoadSingle_ThrowsWhenMultipleProfilesExist()
    {
        var configPath = CreateNamedConfigFile();

        var exception = Assert.Throws<InvalidOperationException>(() => RabbitMqPublishConfigurationLoader.LoadSingle(configPath));

        Assert.Contains("LoadProfile", exception.Message);
    }

    [Fact]
    public void LoadFromFile_ThrowsForNamedProfilesFile()
    {
        var configPath = CreateNamedConfigFile();

        var exception = Assert.Throws<InvalidOperationException>(() => RabbitMqPublishConfigurationLoader.LoadFromFile(configPath));

        Assert.Contains("LoadProfile", exception.Message);
    }

    [Fact]
    public void LoadProfile_ThrowsForUnknownProfile()
    {
        var configPath = CreateNamedConfigFile();

        Assert.Throws<InvalidOperationException>(() => RabbitMqPublishConfigurationLoader.LoadProfile("Unknown", configPath));
    }

    [Fact]
    public void PopulateProfile_FillsExistingConfigurationInstance()
    {
        var configPath = CreateNamedConfigFile();

        var configuration = new RabbitMqPublishConfiguration();
        RabbitMqPublishConfigurationLoader.PopulateProfile(configuration, "OrdersOut", configPath);

        Assert.Equal("OrdersOut", configuration.Name);
        Assert.Equal("orders.outbox", configuration.QueueName);
    }

    [Fact]
    public void PopulateProfile_CopiesSslAndReuseConnectionFields()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqPublish": {
                "OrdersOut": {
                  "HostName": "rabbit.example.com",
                  "Port": 5671,
                  "PublishTarget": "Queue",
                  "QueueName": "orders.outbox",
                  "SslEnabled": true,
                  "SslServerName": "rabbit.example.com",
                  "ReuseConnection": true
                }
              }
            }
            """);

        var configuration = new RabbitMqPublishConfiguration();
        RabbitMqPublishConfigurationLoader.PopulateProfile(configuration, "OrdersOut", configPath);

        Assert.True(configuration.SslEnabled);
        Assert.Equal("rabbit.example.com", configuration.SslServerName);
        Assert.True(configuration.ReuseConnection);
    }

    [Fact]
    public void Validate_ThrowsWhenQueueTargetWithoutQueueName()
    {
        var configuration = new RabbitMqPublishConfiguration
        {
            PublishTarget = RabbitMqPublishTarget.Queue
        };

        Assert.Throws<InvalidOperationException>(() => configuration.Validate());
    }

    [Fact]
    public void Validate_ThrowsWhenExchangeTargetWithoutExchange()
    {
        var configuration = new RabbitMqPublishConfiguration
        {
            PublishTarget = RabbitMqPublishTarget.Exchange
        };

        Assert.Throws<InvalidOperationException>(() => configuration.Validate());
    }

    [Fact]
    public void GetPublishRouting_ResolvesQueueAndExchangeTargets()
    {
        var queueConfiguration = new RabbitMqPublishConfiguration
        {
            PublishTarget = RabbitMqPublishTarget.Queue,
            QueueName = "orders.outbox"
        };
        queueConfiguration.Validate();

        Assert.Equal(string.Empty, queueConfiguration.GetPublishExchange());
        Assert.Equal("orders.outbox", queueConfiguration.GetPublishRoutingKey());

        var exchangeConfiguration = new RabbitMqPublishConfiguration
        {
            PublishTarget = RabbitMqPublishTarget.Exchange,
            Exchange = "integration.events",
            RoutingKey = "order.created"
        };
        exchangeConfiguration.Validate();

        Assert.Equal("integration.events", exchangeConfiguration.GetPublishExchange());
        Assert.Equal("order.created", exchangeConfiguration.GetPublishRoutingKey());
    }

    private static string CreateNamedConfigFile()
    {
        return CreateConfigFile(
            """
            {
              "RabbitMqPublish": {
                "OrdersOut": {
                  "HostName": "localhost",
                  "PublishTarget": "Queue",
                  "QueueName": "orders.outbox",
                  "ClientProvidedName": "IntegrationFlow.OrdersPublisher"
                },
                "EventsOut": {
                  "HostName": "localhost",
                  "PublishTarget": "Exchange",
                  "Exchange": "integration.events",
                  "RoutingKey": "order.created",
                  "ClientProvidedName": "IntegrationFlow.EventsPublisher"
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
