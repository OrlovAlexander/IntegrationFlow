using System;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Exceptions
{
    /// <summary>
    /// Сообщение не может быть доставлено в очередь (mandatory publish без маршрута).
    /// </summary>
    public sealed class UnroutableMessageException : Exception
    {
        public UnroutableMessageException()
            : base("RabbitMQ message is unroutable.")
        {
        }

        public UnroutableMessageException(string message)
            : base(message)
        {
        }
    }
}
