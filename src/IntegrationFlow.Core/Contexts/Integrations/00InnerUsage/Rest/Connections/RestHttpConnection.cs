using System;
using System.Net.Http;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;

/// <summary>
/// REST HTTP connection wrapper for SentAndWait.
/// </summary>
internal sealed class RestHttpConnection : IConnection
{
    public RestHttpConnection(RestRequestReplyConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        HttpClient = RestHttpClientProvider.GetClient(configuration.Name, configuration);
    }

    internal RestRequestReplyConfiguration Configuration { get; }

    internal HttpClient HttpClient { get; }

    public bool NeedReconnect() => false;

    public bool Reconnect() => true;

    public void Dispose()
    {
    }
}
