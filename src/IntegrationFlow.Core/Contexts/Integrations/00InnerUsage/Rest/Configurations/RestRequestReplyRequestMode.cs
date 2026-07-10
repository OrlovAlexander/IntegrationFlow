namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// REST SentAndWait request-reply mode.
/// </summary>
public enum RestRequestReplyRequestMode
{
    /// <summary>
    /// Synchronous blocking HTTP request-response.
    /// </summary>
    Sync,

    /// <summary>
    /// Async outbox: stage pending in TX, relay HTTP request, complete via callback webhook.
    /// </summary>
    AsyncOutbox
}
