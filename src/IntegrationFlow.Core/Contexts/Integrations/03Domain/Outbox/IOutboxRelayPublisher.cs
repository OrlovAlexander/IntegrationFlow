using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox;

/// <summary>
/// Outbox relay transport kind.
/// </summary>
public enum OutboxTransportKind
{
    RabbitMq,
    Rest,
}

/// <summary>
/// Publishes a claimed outbox message through a concrete transport.
/// </summary>
public interface IOutboxRelayPublisher : System.IDisposable
{
    OutboxTransportKind TransportKind { get; }

    ITransmitterWithResult Transmitter { get; }
}
