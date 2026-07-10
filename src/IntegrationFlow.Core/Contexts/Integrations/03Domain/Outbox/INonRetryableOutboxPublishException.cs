namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox;

/// <summary>
/// Marks publish failures that should abandon the outbox message without further relay retries.
/// </summary>
public interface INonRetryableOutboxPublishException
{
}
