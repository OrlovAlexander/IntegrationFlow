using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Истек таймаут ожидания ответа в интеграции SentAndWait.
    /// </summary>
    public sealed class SentAndWaitTimeoutException : Exception
    {
        public SentAndWaitTimeoutException(string message)
            : base(message)
        {
        }

        public SentAndWaitTimeoutException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
