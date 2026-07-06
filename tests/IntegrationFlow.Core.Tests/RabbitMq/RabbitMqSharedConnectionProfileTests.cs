using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations;
using Xunit;

namespace IntegrationFlow.Core.Tests.RabbitMq;

public sealed class RabbitMqSharedConnectionProfileTests : IDisposable
{
    private readonly string? previousSharedHostName;
    private readonly string? previousInboxPassword;

    public RabbitMqSharedConnectionProfileTests()
    {
        previousSharedHostName = Environment.GetEnvironmentVariable("RabbitMqConnections__Prod__HostName");
        previousInboxPassword = Environment.GetEnvironmentVariable("RabbitMq__Inbox__Password");
        RabbitMqConfigurationComposition.ResetOverlayConfiguration();
    }

    public void Dispose()
    {
        RabbitMqConfigurationComposition.ResetOverlayConfiguration();
        Environment.SetEnvironmentVariable("RabbitMqConnections__Prod__HostName", previousSharedHostName);
        Environment.SetEnvironmentVariable("RabbitMq__Inbox__Password", previousInboxPassword);
    }
    [Fact]
    public void LoadProfile_ReceiveAndProcess_AppliesSharedConnection()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqConnections": {
                "Prod": {
                  "HostName": "rabbit.prod.internal",
                  "Port": 5671,
                  "UserName": "integration",
                  "Password": "secret",
                  "VirtualHost": "/prod",
                  "SslEnabled": true,
                  "SslServerName": "rabbit.prod.internal"
                }
              },
              "RabbitMq": {
                "Inbox": {
                  "Connection": "Prod",
                  "QueueName": "integration.inbox",
                  "ClientProvidedName": "IntegrationFlow.InboxListener"
                }
              }
            }
            """);

        var configuration = RabbitMqConfigurationLoader.LoadProfile("Inbox", configPath);

        Assert.Equal("rabbit.prod.internal", configuration.HostName);
        Assert.Equal(5671, configuration.Port);
        Assert.Equal("integration", configuration.UserName);
        Assert.Equal("secret", configuration.Password);
        Assert.Equal("/prod", configuration.VirtualHost);
        Assert.True(configuration.SslEnabled);
        Assert.Equal("rabbit.prod.internal", configuration.SslServerName);
        Assert.Equal("integration.inbox", configuration.QueueName);
        Assert.Equal("IntegrationFlow.InboxListener", configuration.ClientProvidedName);
    }

    [Fact]
    public void LoadProfile_SentAndForgot_AppliesSharedConnection()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqConnections": {
                "Prod": {
                  "HostName": "rabbit.prod.internal",
                  "Password": "secret"
                }
              },
              "RabbitMqPublish": {
                "OrdersOut": {
                  "Connection": "Prod",
                  "QueueName": "orders.out",
                  "PublishTarget": "Queue"
                }
              }
            }
            """);

        var configuration = RabbitMqPublishConfigurationLoader.LoadProfile("OrdersOut", configPath);

        Assert.Equal("rabbit.prod.internal", configuration.HostName);
        Assert.Equal("secret", configuration.Password);
        Assert.Equal("orders.out", configuration.QueueName);
    }

    [Fact]
    public void LoadProfile_SentAndWait_AppliesSharedConnection()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqConnections": {
                "Prod": {
                  "HostName": "rabbit.prod.internal",
                  "SslEnabled": true
                }
              },
              "RabbitMqRequestReply": {
                "OrdersRpc": {
                  "Connection": "Prod",
                  "QueueName": "orders.rpc",
                  "RequestTarget": "Queue"
                }
              }
            }
            """);

        var configuration = RabbitMqRequestReplyConfigurationLoader.LoadProfile("OrdersRpc", configPath);

        Assert.Equal("rabbit.prod.internal", configuration.HostName);
        Assert.True(configuration.SslEnabled);
        Assert.Equal("orders.rpc", configuration.QueueName);
    }

    [Fact]
    public void LoadProfile_ProfileOverridesSharedConnection()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqConnections": {
                "Prod": {
                  "HostName": "rabbit.prod.internal",
                  "Port": 5671
                }
              },
              "RabbitMq": {
                "Inbox": {
                  "Connection": "Prod",
                  "HostName": "rabbit.override.internal",
                  "QueueName": "integration.inbox"
                }
              }
            }
            """);

        var configuration = RabbitMqConfigurationLoader.LoadProfile("Inbox", configPath);

        Assert.Equal("rabbit.override.internal", configuration.HostName);
        Assert.Equal(5671, configuration.Port);
    }

    [Fact]
    public void LoadProfile_SharedConnectionName_IsCaseInsensitive()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqConnections": {
                "Prod": {
                  "HostName": "rabbit.prod.internal"
                }
              },
              "RabbitMq": {
                "Inbox": {
                  "Connection": "prod",
                  "QueueName": "integration.inbox"
                }
              }
            }
            """);

        var previousHostName = Environment.GetEnvironmentVariable("RabbitMqConnections__Prod__HostName");

        try
        {
            Environment.SetEnvironmentVariable("RabbitMqConnections__Prod__HostName", null);

            var configuration = RabbitMqConfigurationLoader.LoadProfile("Inbox", configPath);

            Assert.Equal("rabbit.prod.internal", configuration.HostName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RabbitMqConnections__Prod__HostName", previousHostName);
        }
    }

    [Fact]
    public void LoadProfile_EnvironmentVariableOverridesSharedConnection()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMqConnections": {
                "Prod": {
                  "HostName": "file-host",
                  "Password": "file-secret"
                }
              },
              "RabbitMq": {
                "Inbox": {
                  "Connection": "Prod",
                  "QueueName": "integration.inbox"
                }
              }
            }
            """);

        var previousHostName = Environment.GetEnvironmentVariable("RabbitMqConnections__Prod__HostName");
        var previousPassword = Environment.GetEnvironmentVariable("RabbitMq__Inbox__Password");

        try
        {
            Environment.SetEnvironmentVariable("RabbitMqConnections__Prod__HostName", "env-shared-host");
            Environment.SetEnvironmentVariable("RabbitMq__Inbox__Password", "profile-password");

            var configuration = RabbitMqConfigurationLoader.LoadProfile("Inbox", configPath);

            Assert.Equal("env-shared-host", configuration.HostName);
            Assert.Equal("profile-password", configuration.Password);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RabbitMqConnections__Prod__HostName", previousHostName);
            Environment.SetEnvironmentVariable("RabbitMq__Inbox__Password", previousPassword);
        }
    }

    [Fact]
    public void LoadProfile_ThrowsWhenSharedConnectionMissing()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMq": {
                "Inbox": {
                  "Connection": "Missing",
                  "QueueName": "integration.inbox"
                }
              }
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => RabbitMqConfigurationLoader.LoadProfile("Inbox", configPath));

        Assert.Contains("Missing", exception.Message);
        Assert.Contains("RabbitMqConnections", exception.Message);
    }

    [Fact]
    public void LoadFromFile_LegacyFlatWithoutConnection_RemainsCompatible()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RabbitMq": {
                "HostName": "localhost",
                "QueueName": "integration.inbox"
              }
            }
            """);

        var configuration = RabbitMqConfigurationLoader.LoadFromFile(configPath);

        Assert.Equal("localhost", configuration.HostName);
        Assert.Equal("integration.inbox", configuration.QueueName);
    }

    private static string CreateConfigFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(filePath, content);
        return filePath;
    }
}
