using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using IntegrationFlow.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationFlow.Core.Tests.RabbitMq;

public sealed class RabbitMqConfigurationOverlayTests : IDisposable
{
    private readonly string? previousHostNameVariable;

    public RabbitMqConfigurationOverlayTests()
    {
        previousHostNameVariable = Environment.GetEnvironmentVariable("RabbitMq__Inbox__HostName");
        RabbitMqConfigurationComposition.ResetOverlayConfiguration();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("RabbitMq__Inbox__HostName", previousHostNameVariable);
        RabbitMqConfigurationComposition.ResetOverlayConfiguration();
    }

    [Fact]
    public void LoadProfile_EnvironmentVariableOverridesJsonHostName()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMq": {
                "Inbox": {
                  "HostName": "file-host",
                  "QueueName": "integration.inbox"
                }
              }
            }
            """);

        Environment.SetEnvironmentVariable("RabbitMq__Inbox__HostName", "env-host");

        var configuration = RabbitMqConfigurationLoader.LoadProfile("Inbox", configPath);

        Assert.Equal("env-host", configuration.HostName);
        Assert.Equal("integration.inbox", configuration.QueueName);
    }

    [Fact]
    public void LoadProfile_IConfigurationOverlayOverridesJson()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqPublish": {
                "OrdersOut": {
                  "HostName": "file-host",
                  "QueueName": "orders.out",
                  "PublishTarget": "Queue"
                }
              }
            }
            """);

        var overlay = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMqPublish:OrdersOut:HostName"] = "overlay-host",
                ["RabbitMqPublish:OrdersOut:Password"] = "overlay-secret"
            })
            .Build();

        RabbitMqConfigurationComposition.OverlayConfiguration = overlay;

        var configuration = RabbitMqPublishConfigurationLoader.LoadProfile("OrdersOut", configPath);

        Assert.Equal("overlay-host", configuration.HostName);
        Assert.Equal("overlay-secret", configuration.Password);
    }

    [Fact]
    public void LoadProfile_FromIConfigurationWithoutFile()
    {
        var configurationRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMqRequestReply:InventoryRpc:HostName"] = "rpc-host",
                ["RabbitMqRequestReply:InventoryRpc:QueueName"] = "inventory.rpc",
                ["RabbitMqRequestReply:InventoryRpc:RequestTarget"] = "Queue",
                ["RabbitMqRequestReply:InventoryRpc:ReplyMode"] = "DirectReplyTo"
            })
            .Build();

        var configuration = RabbitMqRequestReplyConfigurationLoader.LoadProfile("InventoryRpc", configurationRoot);

        Assert.Equal("rpc-host", configuration.HostName);
        Assert.Equal("inventory.rpc", configuration.QueueName);
    }

    [Fact]
    public void AddIntegrationFlowRabbitMq_SetsOverlayConfiguration()
    {
        var overlay = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:Inbox:HostName"] = "di-host"
            })
            .Build();

        new ServiceCollection()
            .AddIntegrationFlowRabbitMq(overlay);

        Assert.Same(overlay, RabbitMqConfigurationComposition.OverlayConfiguration);
    }

    private static string CreateConfigFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(filePath, content);
        return filePath;
    }
}
