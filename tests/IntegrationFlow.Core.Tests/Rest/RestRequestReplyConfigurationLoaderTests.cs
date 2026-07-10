using System;
using System.IO;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using Xunit;

namespace IntegrationFlow.Core.Tests.Rest;

public sealed class RestRequestReplyConfigurationLoaderTests : IDisposable
{
    public void Dispose()
    {
        RestConfigurationComposition.ResetOverlayConfiguration();
    }

    [Fact]
    public void LoadFromFile_ReadsLegacyFlatSettings()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RestRequestReply": {
                "BaseAddress": "https://api.example.com/",
                "RequestPath": "/v1/lookup",
                "Method": "PUT",
                "ContentType": "text/plain",
                "Accept": "application/json",
                "ResponseTimeoutSeconds": 12,
                "MaxConcurrentRequests": 3,
                "IdempotencyHeaderName": "X-Idempotency-Key",
                "BearerToken": "token"
              }
            }
            """);

        var configuration = RestRequestReplyConfigurationLoader.LoadFromFile(configPath);

        Assert.Equal(RestRequestReplyConfigurationLoader.DefaultProfileName, configuration.Name);
        Assert.Equal("https://api.example.com/", configuration.BaseAddress);
        Assert.Equal("/v1/lookup", configuration.RequestPath);
        Assert.Equal("PUT", configuration.Method);
        Assert.Equal("text/plain", configuration.ContentType);
        Assert.Equal(12, configuration.ResponseTimeoutSeconds);
        Assert.Equal(3, configuration.MaxConcurrentRequests);
        Assert.Equal("X-Idempotency-Key", configuration.IdempotencyHeaderName);
        Assert.Equal("token", configuration.BearerToken);
    }

    [Fact]
    public void LoadAll_ReadsNamedProfiles()
    {
        var configPath = CreateNamedConfigFile();
        var profiles = RestRequestReplyConfigurationLoader.LoadAll(configPath);

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, profile => profile.Name == "OrdersLookup" && profile.RequestPath == "/v1/orders/lookup");
        Assert.Contains(profiles, profile => profile.Name == "InventoryLookup" && profile.RequestPath == "/v1/inventory");
    }

    [Fact]
    public void LoadProfile_ReadsNamedProfileByName()
    {
        var configPath = CreateNamedConfigFile();
        var configuration = RestRequestReplyConfigurationLoader.LoadProfile("InventoryLookup", configPath);

        Assert.Equal("InventoryLookup", configuration.Name);
        Assert.Equal("https://inventory.example.com/", configuration.BaseAddress);
        Assert.Equal("/v1/inventory", configuration.RequestPath);
    }

    [Fact]
    public void LoadProfile_AppliesSharedConnection()
    {
        var configPath = CreateConfigFile(
            """
            {
              "RestConnections": {
                "PartnerApi": {
                  "BaseAddress": "https://partner.example.com/",
                  "BearerToken": "shared-token",
                  "Accept": "application/vnd.api+json"
                }
              },
              "RestRequestReply": {
                "OrdersLookup": {
                  "Connection": "PartnerApi",
                  "RequestPath": "/orders/lookup",
                  "ResponseTimeoutSeconds": 20
                }
              }
            }
            """);

        var configuration = RestRequestReplyConfigurationLoader.LoadProfile("OrdersLookup", configPath);

        Assert.Equal("https://partner.example.com/", configuration.BaseAddress);
        Assert.Equal("shared-token", configuration.BearerToken);
        Assert.Equal("application/vnd.api+json", configuration.Accept);
        Assert.Equal("/orders/lookup", configuration.RequestPath);
        Assert.Equal(20, configuration.ResponseTimeoutSeconds);
    }

    [Fact]
    public void BuildRequestUri_CombinesBaseAddressAndPath()
    {
        var configuration = new RestRequestReplyConfiguration
        {
            BaseAddress = "https://api.example.com",
            RequestPath = "/v1/orders",
        };

        var uri = configuration.BuildRequestUri();

        Assert.Equal("https://api.example.com/v1/orders", uri.ToString());
    }

    private static string CreateNamedConfigFile()
        => CreateConfigFile(
            """
            {
              "RestRequestReply": {
                "OrdersLookup": {
                  "BaseAddress": "https://api.example.com/",
                  "RequestPath": "/v1/orders/lookup"
                },
                "InventoryLookup": {
                  "BaseAddress": "https://inventory.example.com/",
                  "RequestPath": "/v1/inventory"
                }
              }
            }
            """);

    private static string CreateConfigFile(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rest-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
