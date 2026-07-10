using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Transmitters;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace IntegrationFlow.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RestHttpEndToEndTests : IAsyncLifetime, IDisposable
{
    private WireMockServer? server;

    public Task InitializeAsync()
    {
        server = WireMockServer.Start();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        server?.Dispose();
        RestHttpClientProvider.Reset();
        RestConfigurationComposition.ResetOverlayConfiguration();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        server?.Dispose();
        RestHttpClientProvider.Reset();
        RestConfigurationComposition.ResetOverlayConfiguration();
    }

    [Fact]
    public async Task IntegrateWithResultAsync_Roundtrip_ReturnsResponseBody()
    {
        server!.Given(Request.Create().WithPath("/v1/orders/lookup").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"orderId\":42}"));

        var configuration = CreateConfiguration(server.Url!);
        var transmitter = CreateTransmitter(configuration);
        var result = await transmitter.TransmitAsync(
            new TransmitData("{\"query\":\"x\"}", "idem-42"),
            CancellationToken.None);

        Assert.False(result.IsFailed);
        Assert.Equal("{\"orderId\":42}", result.Data);
    }

    [Fact]
    public async Task IntegrateWithResultAsync_Timeout_ThrowsSentAndWaitTimeoutException()
    {
        server!.Given(Request.Create().WithPath("/v1/orders/lookup").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithDelay(TimeSpan.FromSeconds(2))
                .WithBody("late"));

        var configuration = CreateConfiguration(server.Url!, responseTimeoutSeconds: 1);
        var transmitter = CreateTransmitter(configuration);

        await Assert.ThrowsAsync<SentAndWaitTimeoutException>(() =>
            transmitter.TransmitAsync(new TransmitData("{\"query\":\"x\"}"), CancellationToken.None));
    }

    [Fact]
    public void LoadProfile_FromTempConfig_WorksWithWireMockBaseAddress()
    {
        var configPath = CreateConfigFile(
            $$"""
            {
              "RestRequestReply": {
                "OrdersLookup": {
                  "BaseAddress": "{{server!.Url}}/",
                  "RequestPath": "/v1/orders/lookup",
                  "ResponseTimeoutSeconds": 5
                }
              }
            }
            """);

        var configuration = RestRequestReplyConfigurationLoader.LoadProfile("OrdersLookup", configPath);

        Assert.Equal($"{server.Url}/", configuration.BaseAddress);
        Assert.Equal("/v1/orders/lookup", configuration.RequestPath);
    }

    private static RestRequestReplyConfiguration CreateConfiguration(
        string baseAddress,
        int responseTimeoutSeconds = 5)
        => new()
        {
            Name = "OrdersLookup",
            BaseAddress = baseAddress,
            RequestPath = "/v1/orders/lookup",
            Method = "POST",
            ResponseTimeoutSeconds = responseTimeoutSeconds,
        };

    private static RestHttpTransmitter CreateTransmitter(RestRequestReplyConfiguration configuration)
    {
        RestHttpClientProvider.Reset();
        var connection = new RestHttpConnection(configuration);
        return new RestHttpTransmitter(configuration, connection);
    }

    private static string CreateConfigFile(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rest-e2e-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
