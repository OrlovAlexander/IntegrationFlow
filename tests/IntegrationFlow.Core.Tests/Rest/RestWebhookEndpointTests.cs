using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.ReceiveAndProcess.Messages;
using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace IntegrationFlow.Core.Tests.Rest;

public sealed class RestWebhookEndpointTests
{
    [Fact]
    public async Task MapIntegrationFlowWebhook_Returns200OnSuccess()
    {
        RestWebhookReceivedMessage? received = null;
        var path = $"/integrations/webhooks/test-{Guid.NewGuid():N}";
        using var host = await CreateHostAsync(path, (message, _) =>
        {
            received = message;
            return Task.CompletedTask;
        });

        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent("{\"id\":1}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Webhook-Id", "endpoint-1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(received);
        Assert.Equal("endpoint-1", received!.MessageId);
        Assert.Contains("\"id\":1", received.BodyText);
    }

    [Fact]
    public async Task MapIntegrationFlowWebhook_Returns500WhenHandlerFails()
    {
        var path = $"/integrations/webhooks/test-{Guid.NewGuid():N}";
        using var host = await CreateHostAsync(path, (_, _) =>
            Task.FromException(new InvalidOperationException("handler failed")));

        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent("payload", Encoding.UTF8, "application/json"),
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private static async Task<IHost> CreateHostAsync(
        string path,
        Func<RestWebhookReceivedMessage, CancellationToken, Task> handler)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton<IIntegrationLogger>(_ => NullIntegrationLogger.Instance);
                        services.AddRouting();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapIntegrationFlowWebhook("OrdersInbox", path, handler);
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }
}
