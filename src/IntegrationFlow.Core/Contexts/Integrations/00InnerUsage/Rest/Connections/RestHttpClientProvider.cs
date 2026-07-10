using System;
using System.Collections.Concurrent;
using System.Net.Http;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using Microsoft.Extensions.Http;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;

/// <summary>
/// Provides <see cref="HttpClient"/> instances for REST profiles.
/// </summary>
internal static class RestHttpClientProvider
{
    private static readonly ConcurrentDictionary<string, HttpClient> StandaloneClients = new(StringComparer.OrdinalIgnoreCase);
    private static IHttpClientFactory? httpClientFactory;

    internal static void Initialize(IHttpClientFactory? factory)
        => httpClientFactory = factory;

    internal static void Reset()
    {
        httpClientFactory = null;
        foreach (var client in StandaloneClients.Values)
        {
            client.Dispose();
        }

        StandaloneClients.Clear();
    }

    public static HttpClient GetClient(RestRequestReplyConfiguration configuration)
        => GetClient(configuration.Name, configuration);

    public static HttpClient GetClient(RestPublishConfiguration configuration)
        => GetClient(configuration.Name, configuration);

    public static HttpClient GetClient(string profileName, IRestConnectionConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("REST profile name is required.", nameof(profileName));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var clientName = BuildClientName(profileName);
        if (httpClientFactory != null)
        {
            return httpClientFactory.CreateClient(clientName);
        }

        return StandaloneClients.GetOrAdd(
            clientName,
            _ => CreateStandaloneClient(configuration));
    }

    internal static HttpClient CreateStandaloneClient(IRestConnectionConfiguration configuration)
    {
        var handler = RestHttpClientHandlerFactory.CreateHandler(configuration);
        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    internal static void RegisterTestClient(string profileName, HttpClient client)
    {
        StandaloneClients[BuildClientName(profileName)] = client;
    }

    internal static string BuildClientName(string profileName)
        => $"IntegrationFlow.Rest.{profileName}";
}
