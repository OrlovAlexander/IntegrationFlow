using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Cache;

/// <summary>
/// Optional client-side cache for idempotent REST SentAndWait responses.
/// </summary>
public interface IRestClientResponseCache
{
    Task<byte[]?> TryGetAsync(string profileName, string messageId, CancellationToken cancellationToken = default);

    Task StoreAsync(
        string profileName,
        string messageId,
        byte[] responseBody,
        CancellationToken cancellationToken = default);
}
