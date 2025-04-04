namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess
{
    /// <summary>
    /// Входящее сообщение, запрос и т.п.
    /// </summary>
    public class InboxMessage
    {
        public object Message { get; private set; }

        public bool IsFailed { get; private set; }

        public InboxMessage(object inboxMessage)
        {
            Message = inboxMessage;
            IsFailed = false;
        }

        public InboxMessage(object inboxMessage, bool isFailed)
        {
            Message = inboxMessage;
            IsFailed = isFailed;
        }

        public void SetFailed()
        {
            IsFailed = true;
        }
    }
}
