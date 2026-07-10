using System;
using System.IO;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using Xunit;

namespace IntegrationFlow.Core.Tests.Rest;

public sealed class RestWebhookConfigurationLoaderTests : IDisposable
{
    public void Dispose()
    {
        RestConfigurationComposition.ResetOverlayConfiguration();
    }

    [Fact]
    public void LoadAll_ReadsNamedProfiles()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RestWebhooks": {
                "OrdersInbox": {
                  "Path": "/integrations/webhooks/orders",
                  "MessageIdHeaderName": "X-Webhook-Id"
                },
                "PaymentsInbox": {
                  "Path": "/integrations/webhooks/payments",
                  "RequireMessageId": true
                }
              }
            }
            """);

        var profiles = RestWebhookConfigurationLoader.LoadAll(configPath);

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, profile =>
            profile.Name == "OrdersInbox" && profile.Path == "/integrations/webhooks/orders");
        Assert.Contains(profiles, profile =>
            profile.Name == "PaymentsInbox" && profile.RequireMessageId);
    }

    [Fact]
    public void LoadProfile_ReadsNamedProfileByName()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RestWebhooks": {
                "OrdersInbox": {
                  "Path": "/integrations/webhooks/orders",
                  "MaxBodyBytes": 2048
                }
              }
            }
            """);

        var configuration = RestWebhookConfigurationLoader.LoadProfile("OrdersInbox", configPath);

        Assert.Equal("OrdersInbox", configuration.Name);
        Assert.Equal("/integrations/webhooks/orders", configuration.Path);
        Assert.Equal(2048, configuration.MaxBodyBytes);
    }

    [Fact]
    public void LoadProfile_LegacyFlatFormat_UsesDefaultProfileName()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RestWebhooks": {
                "Path": "/integrations/webhooks/default",
                "AllowedMethods": ["POST", "PUT"]
              }
            }
            """);

        var configuration = RestWebhookConfigurationLoader.LoadProfile(
            RestWebhookConfigurationLoader.DefaultProfileName,
            configPath);

        Assert.Equal(RestWebhookConfigurationLoader.DefaultProfileName, configuration.Name);
        Assert.Equal("/integrations/webhooks/default", configuration.Path);
        Assert.Contains("POST", configuration.AllowedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("PUT", configuration.AllowedMethods, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ThrowsWhenPathMissingLeadingSlash()
    {
        var configuration = new RestWebhookConfiguration
        {
            Name = "Invalid",
            Path = "integrations/webhooks/orders",
        };

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.Validate());
        Assert.Contains("Path must start with '/'", exception.Message);
    }

    private static string CreateConfigFile(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rest-webhook-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
