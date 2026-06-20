using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq;

public sealed class RabbitMqConfigurationLoaderTests
{
    [Fact]
    public void Load_ReadsSettingsFromJsonFile()
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
                "ClientProvidedName": "IntegrationFlow.TestListener"
              }
            }
            """);

        var configuration = RabbitMqConfigurationLoader.LoadFromFile(configPath);

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
    }

    [Fact]
    public void Populate_FillsExistingConfigurationInstance()
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
        RabbitMqConfigurationLoader.Populate(configuration, configPath);

        Assert.Equal("127.0.0.1", configuration.HostName);
        Assert.Equal("events.inbox", configuration.QueueName);
        Assert.Equal(5672, configuration.Port);
    }

    [Fact]
    public void Load_ThrowsWhenFileDoesNotExist()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        Assert.Throws<FileNotFoundException>(() => RabbitMqConfigurationLoader.LoadFromFile(missingPath));
    }

    private static string CreateConfigFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(filePath, content);
        return filePath;
    }
}
