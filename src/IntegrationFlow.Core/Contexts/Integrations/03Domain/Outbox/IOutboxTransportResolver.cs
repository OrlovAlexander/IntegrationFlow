namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox;

/// <summary>
/// Resolves outbox relay transport by profile name.
/// </summary>
public interface IOutboxTransportResolver
{
    IOutboxRelayPublisher CreatePublisher(string profileName);
}
