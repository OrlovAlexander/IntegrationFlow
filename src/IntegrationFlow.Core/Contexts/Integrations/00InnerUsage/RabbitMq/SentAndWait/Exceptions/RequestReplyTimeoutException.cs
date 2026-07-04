using System;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Exceptions
{
    /// <summary>
    /// Истек таймаут ожидания RPC-ответа от RabbitMQ.
    /// </summary>
    public sealed class RequestReplyTimeoutException : Exception
    {
        public RequestReplyTimeoutException(string message)
            : base(message)
        {
        }

        public RequestReplyTimeoutException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
