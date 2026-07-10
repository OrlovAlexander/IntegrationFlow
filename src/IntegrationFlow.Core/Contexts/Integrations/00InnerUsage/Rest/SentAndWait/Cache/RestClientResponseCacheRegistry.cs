namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Cache;

internal static class RestClientResponseCacheRegistry
{
    private static IRestClientResponseCache? cache;

    internal static void Initialize(IRestClientResponseCache? responseCache)
        => cache = responseCache;

    internal static void Reset()
        => cache = null;

    internal static IRestClientResponseCache? Instance => cache;
}
