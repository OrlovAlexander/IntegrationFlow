using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;

/// <summary>
/// Adapter from delegate to <see cref="IInboxMessageProcessing"/>.
/// </summary>
public sealed class DelegateInboxMessageProcessing : IInboxMessageProcessing
{
    private readonly Action<InboxMessage> handler;

    public DelegateInboxMessageProcessing(Action<InboxMessage> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <inheritdoc />
    public void ProcessInboxMessage(InboxMessage inboxMessage)
    {
        handler(inboxMessage);
    }
}
