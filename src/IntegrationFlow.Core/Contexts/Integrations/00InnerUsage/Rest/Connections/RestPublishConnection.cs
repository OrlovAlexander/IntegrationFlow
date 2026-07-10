using System.Net.Http;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;

/// <summary>
/// REST HTTP connection wrapper for SentAndForgot publish.
/// </summary>
internal sealed class RestPublishConnection : IConnection
{
    public RestPublishConnection(RestPublishConfiguration configuration)
    {
        Configuration = configuration ?? throw new System.ArgumentNullException(nameof(configuration));
        HttpClient = RestHttpClientProvider.GetClient(configuration);
    }

    internal RestPublishConfiguration Configuration { get; }

    internal HttpClient HttpClient { get; }

    public bool NeedReconnect() => false;

    public bool Reconnect() => true;

    public void Dispose()
    {
    }
}
