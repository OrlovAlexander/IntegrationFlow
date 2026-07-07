using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq;

public sealed class RabbitMqConfigurationLoaderTests
{
    [Fact]
    public void LoadFromFile_ReadsLegacyFlatSettings()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMq": {
                "HostName": "rabbit.example.com",
                "Port": 5673,
                "UserName": "integration",
                "Password": "secret",
                "VirtualHost": "/prod",
                "QueueName": "orders.inbox",
                "PrefetchCount": 5,
                "Asynchronously": false,
                "AutomaticRecoveryEnabled": false,
                "ClientProvidedName": "IntegrationFlow.TestListener",
                "ValidateTopology": false,
                "DeclareTopologyOnStartup": true
              }
            }
            """);

        var configuration = RabbitMqConfigurationLoader.LoadFromFile(configPath);

        Assert.Equal(RabbitMqConfigurationLoader.DefaultProfileName, configuration.Name);
        Assert.Equal("rabbit.example.com", configuration.HostName);
        Assert.Equal(5673, configuration.Port);
        Assert.Equal("integration", configuration.UserName);
        Assert.Equal("secret", configuration.Password);
        Assert.Equal("/prod", configuration.VirtualHost);
        Assert.Equal("orders.inbox", configuration.QueueName);
        Assert.Equal((ushort)5, configuration.PrefetchCount);
        Assert.False(configuration.Asynchronously);
        Assert.False(configuration.AutomaticRecoveryEnabled);
        Assert.Equal("IntegrationFlow.TestListener", configuration.ClientProvidedName);
        Assert.False(configuration.ValidateTopology);
        Assert.True(configuration.DeclareTopologyOnStartup);
    }

    [Fact]
    public void LoadAll_ReadsNamedProfiles()
    {
        var configPath = CreateNamedConfigFile();

        var profiles = RabbitMqConfigurationLoader.LoadAll(configPath);

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, profile => profile.Name == "Inbox" && profile.QueueName == "integration.inbox");
        Assert.Contains(profiles, profile => profile.Name == "Orders" && profile.QueueName == "orders.inbox");
    }

    [Fact]
    public void LoadProfile_ReadsNamedProfileByName()
    {
        var configPath = CreateNamedConfigFile();

        var configuration = RabbitMqConfigurationLoader.LoadProfile("Orders", configPath);

        Assert.Equal("Orders", configuration.Name);
        Assert.Equal("orders.inbox", configuration.QueueName);
        Assert.Equal("IntegrationFlow.OrdersListener", configuration.ClientProvidedName);
    }

    [Fact]
    public void Load_ReturnsSingleProfileFromNamedFileWhenOnlyOneProfileExists()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMq": {
                "Inbox": {
                  "HostName": "localhost",
                  "QueueName": "integration.inbox"
                }
              }
            }
            """);

        var configuration = RabbitMqConfigurationLoader.LoadSingle(configPath);

        Assert.Equal("Inbox", configuration.Name);
        Assert.Equal("integration.inbox", configuration.QueueName);
    }

    [Fact]
    public void Load_ThrowsWhenMultipleProfilesExist()
    {
        var configPath = CreateNamedConfigFile();

        var exception = Assert.Throws<InvalidOperationException>(() => RabbitMqConfigurationLoader.LoadSingle(configPath));

        Assert.Contains("LoadProfile", exception.Message);
    }

    [Fact]
    public void LoadFromFile_ThrowsForNamedProfilesFile()
    {
        var configPath = CreateNamedConfigFile();

        var exception = Assert.Throws<InvalidOperationException>(() => RabbitMqConfigurationLoader.LoadFromFile(configPath));

        Assert.Contains("LoadProfile", exception.Message);
    }

    [Fact]
    public void LoadProfile_ThrowsForUnknownProfile()
    {
        var configPath = CreateNamedConfigFile();

        Assert.Throws<InvalidOperationException>(() => RabbitMqConfigurationLoader.LoadProfile("Unknown", configPath));
    }

    [Fact]
    public void PopulateFromFile_FillsExistingConfigurationInstance()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMq": {
                "HostName": "127.0.0.1",
                "QueueName": "events.inbox"
              }
            }
            """);

        var configuration = new RabbitMqConfiguration();
        RabbitMqConfigurationLoader.PopulateFromFile(configuration, configPath);

        Assert.Equal(RabbitMqConfigurationLoader.DefaultProfileName, configuration.Name);
        Assert.Equal("127.0.0.1", configuration.HostName);
        Assert.Equal("events.inbox", configuration.QueueName);
        Assert.Equal(5672, configuration.Port);
    }

    [Fact]
    public void PopulateProfile_FillsExistingConfigurationInstance()
    {
        var configPath = CreateNamedConfigFile();

        var configuration = new RabbitMqConfiguration();
        RabbitMqConfigurationLoader.PopulateProfile(configuration, "Inbox", configPath);

        Assert.Equal("Inbox", configuration.Name);
        Assert.Equal("integration.inbox", configuration.QueueName);
    }

    [Fact]
    public void PopulateProfile_CopiesSslFields()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMq": {
                "Inbox": {
                  "HostName": "rabbit.example.com",
                  "Port": 5671,
                  "QueueName": "integration.inbox",
                  "SslEnabled": true,
                  "SslServerName": "rabbit.example.com"
                }
              }
            }
            """);

        var configuration = new RabbitMqConfiguration();
        RabbitMqConfigurationLoader.PopulateProfile(configuration, "Inbox", configPath);

        Assert.True(configuration.SslEnabled);
        Assert.Equal("rabbit.example.com", configuration.SslServerName);
        Assert.Equal(5671, configuration.Port);
    }

    [Fact]
    public void PopulateProfile_CopiesRetryPolicyFields()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMq": {
                "Inbox": {
                  "HostName": "localhost",
                  "QueueName": "integration.inbox",
                  "RequeueOnFailure": true,
                  "MaxRetryCount": 5
                }
              }
            }
            """);

        var configuration = new RabbitMqConfiguration { RequeueOnFailure = false, MaxRetryCount = 0 };
        RabbitMqConfigurationLoader.PopulateProfile(configuration, "Inbox", configPath);

        Assert.True(configuration.RequeueOnFailure);
        Assert.Equal(5, configuration.MaxRetryCount);
    }

    [Fact]
    public void LoadFromFile_ThrowsWhenFileDoesNotExist()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        Assert.Throws<FileNotFoundException>(() => RabbitMqConfigurationLoader.LoadFromFile(missingPath));
    }

    private static string CreateNamedConfigFile()
    {
        return CreateConfigFile(
            """
            {
              "RabbitMq": {
                "Inbox": {
                  "HostName": "localhost",
                  "QueueName": "integration.inbox",
                  "ClientProvidedName": "IntegrationFlow.InboxListener"
                },
                "Orders": {
                  "HostName": "localhost",
                  "QueueName": "orders.inbox",
                  "ClientProvidedName": "IntegrationFlow.OrdersListener"
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
